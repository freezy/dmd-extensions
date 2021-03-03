#version 130

in vec4 Position;
in vec2 TexCoord;

out vec2 uv;

void main()
{
	uv = vec2(TexCoord.x, 1.0 - TexCoord.y);
    gl_Position = Position;
}