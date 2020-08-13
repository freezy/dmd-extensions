#version 130

in vec4 Position;
in vec2 TexCoord;

out vec2 uv;

void main()
{
	uv = TexCoord;
    gl_Position = Position;
}