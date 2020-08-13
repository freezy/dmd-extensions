#version 130

in vec4 Position;
in vec2 TexCoord;

out vec2 dmdUv;
out vec2 glassUv;

uniform vec2 glassTexOffset; // Offset and scale of DMD inside Glass
uniform vec2 glassTexScale; // Scale for margin glass

void main()
{
	dmdUv = TexCoord * glassTexScale - glassTexOffset;
	glassUv = TexCoord;
    gl_Position = Position;
}