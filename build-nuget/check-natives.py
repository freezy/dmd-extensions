#!/usr/bin/env python3
"""Checks that every native library of a LibDmd.Core.Native.<rid> package loads on its platform.

Usage: check-natives.py <LibDmd.Core.Native.<rid>.<version>.nupkg | runtimes/<rid>/native folder>

Run it on the package's platform:
- Windows: every DLL's imports (including the delay-loaded ones) are in the package or in the system, and it loads.
- macOS: every dylib's signature verifies (codesign --verify --strict), and it loads. Unsigned x86_64-only dylibs pass:
  macOS runs unsigned x86_64 code, only arm64 code must be signed.
- Linux: every library loads.

Each library loads in a process of its own, so a library loaded earlier can't stand in for a missing file. The folder is
on the library path (DYLD_LIBRARY_PATH, LD_LIBRARY_PATH), as when the libraries are loaded as plugins. Exits with 1 when
a library fails.
"""

import ctypes
import os
import re
import struct
import subprocess
import sys
import tempfile
import zipfile


def is_native(name):
	if sys.platform == "win32":
		return name.lower().endswith(".dll")
	if sys.platform == "darwin":
		return name.endswith(".dylib")
	return re.search(r"\.so(\.\d+)*$", name) is not None


def pe_imports(path):
	"""The DLLs a PE file imports, from its import and delay-load import directories."""
	with open(path, "rb") as file:
		data = file.read()
	pe = struct.unpack_from("<I", data, 0x3C)[0]
	sections, optional_size = struct.unpack_from("<H", data, pe + 6)[0], struct.unpack_from("<H", data, pe + 20)[0]
	optional = pe + 24
	directories = optional + (112 if struct.unpack_from("<H", data, optional)[0] == 0x20B else 96)
	table = [struct.unpack_from("<IIII", data, optional + optional_size + 40 * i + 8) for i in range(sections)]

	def offset(rva):
		for size, address, raw_size, raw in table:
			if address <= rva < address + max(size, raw_size):
				return rva - address + raw
		raise ValueError(f"RVA {rva:#x} is in no section")

	def string(rva):
		start = offset(rva)
		return data[start:data.index(b"\0", start)].decode("ascii")

	names = []
	# import directory (1): 20-byte descriptors, the name RVA at +12
	# delay-load import directory (13): 32-byte descriptors, the name RVA at +4
	for index, entry_size, name_field in ((1, 20, 12), (13, 32, 4)):
		rva = struct.unpack_from("<I", data, directories + 8 * index)[0]
		if rva == 0:
			continue
		entry = offset(rva)
		while True:
			fields = data[entry:entry + entry_size]
			if fields == bytes(entry_size):
				break
			names.append(string(struct.unpack_from("<I", fields, name_field)[0]))
			entry += entry_size
	return names


def missing_imports(folder, name):
	"""The imports of a DLL that neither the package nor the system has."""
	local = {entry.lower() for entry in os.listdir(folder)}
	system = os.path.join(os.environ.get("SystemRoot", r"C:\Windows"), "System32")
	missing = []
	for dll in pe_imports(os.path.join(folder, name)):
		lower = dll.lower()
		if lower in local or lower.startswith(("api-ms-win-", "ext-ms-")) or os.path.isfile(os.path.join(system, dll)):
			continue
		missing.append(dll)
	return missing


def macho_archs(path):
	"""The architectures of a Mach-O file."""
	names = {0x01000007: "x86_64", 0x0100000C: "arm64"}
	with open(path, "rb") as file:
		header = file.read(4096)
	if struct.unpack_from(">I", header, 0)[0] == 0xCAFEBABE:
		count = struct.unpack_from(">I", header, 4)[0]
		cpus = [struct.unpack_from(">I", header, 8 + 20 * i)[0] for i in range(count)]
	else:
		cpus = [struct.unpack_from("<I", header, 4)[0]]
	return [names.get(cpu, hex(cpu)) for cpu in cpus]


def load(path):
	"""Loads one library in a process of its own: None when it loads, the error otherwise."""
	env = dict(os.environ)
	if sys.platform != "win32":
		variable = "DYLD_LIBRARY_PATH" if sys.platform == "darwin" else "LD_LIBRARY_PATH"
		env[variable] = os.pathsep.join(filter(None, [os.path.dirname(path), env.get(variable)]))
	result = subprocess.run([sys.executable, __file__, "--load", path], env=env, capture_output=True, text=True)
	return None if result.returncode == 0 else ((result.stdout + result.stderr).strip() or f"exited with code {result.returncode}")


def check(folder):
	names = sorted(name for name in os.listdir(folder) if is_native(name) and os.path.isfile(os.path.join(folder, name)))
	if not names:
		print(f"FAIL: no native libraries in {folder}")
		return False
	failed = 0
	for name in names:
		path = os.path.join(folder, name)
		problems = []
		if sys.platform == "win32":
			missing = missing_imports(folder, name)
			if missing:
				problems.append(f"imports {', '.join(missing)}, which neither the package nor the system has")
		if sys.platform == "darwin":
			result = subprocess.run(["codesign", "--verify", "--strict", "--verbose=2", path], capture_output=True, text=True)
			output = (result.stdout + result.stderr).strip()
			# macOS runs unsigned x86_64 code, but arm64 code must be signed
			unsigned_intel = "not signed at all" in output and "arm64" not in macho_archs(path)
			if result.returncode != 0 and not unsigned_intel:
				problems.append(f"invalid signature: {output}")
		error = load(path)
		if error:
			problems.append(f"does not load: {error}")
		if problems:
			failed += 1
			for problem in problems:
				print(f"FAIL: {name} {problem}")
		else:
			print(f"ok: {name}")
	print(f"{len(names) - failed} of {len(names)} libraries load" + ("" if failed == 0 else f", {failed} fail"))
	return failed == 0


def main():
	if len(sys.argv) == 3 and sys.argv[1] == "--load":
		try:
			ctypes.CDLL(sys.argv[2])
		except OSError as error:
			print(error)
			return 1
		return 0
	if len(sys.argv) != 2:
		print(__doc__)
		return 2
	source = sys.argv[1]
	if os.path.isdir(source):
		return 0 if check(source) else 1
	with tempfile.TemporaryDirectory() as temp, zipfile.ZipFile(source) as package:
		folders = set()
		for member in package.namelist():
			match = re.match(r"runtimes/[^/]+/native/[^/]+$", member)
			if match:
				package.extract(member, temp)
				folders.add(os.path.dirname(member))
		if len(folders) != 1:
			print(f"FAIL: expected the natives of one runtime in {source}, found {sorted(folders) or 'none'}")
			return 1
		return 0 if check(os.path.join(temp, folders.pop())) else 1


if __name__ == "__main__":
	sys.exit(main())
