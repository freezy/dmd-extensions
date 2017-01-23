var url = "ws://localhost:9090/dmd";
var output, websocket;

function init() {
	output = document.getElementById("output");
	websocket = new ReconnectingWebSocket(url, null, { binaryType: 'arraybuffer' });

	websocket.onopen = function (e) {
		send('init');
	};

	websocket.onmessage = function (e) {
		var data = new DataView(e.data);
		var event = data.getUint8(0);
		switch (event) {
			case 0x1:
				onSetDimensions(new DataView(e.data.slice(1)));
				break;
		}
	};

	websocket.onerror = function (e) {
		onError(e);
	};

	websocket.onclose = function (e) {
		onClose(e);
	};
}

function onSetDimensions(data) {
	var width = data.getInt32(0, true);
	var height = data.getInt32(4, true);
	console.log('Got dimensions: %sx%s', width, height);
}
function onError(event) {
	writeToScreen('<span style="color: red;">ERROR: ' + event.data + '</span>');
}

function onClose(event) {
	writeToScreen("DISCONNECTED");
}

function send(message) {
	writeToScreen("SENT: " + message);
	websocket.send(message);
}

function writeToScreen(message) {
	var pre = document.createElement("p");
	pre.style.wordWrap = "break-word";
	pre.innerHTML = message;
	output.appendChild(pre);
}

window.addEventListener("load", init, false);