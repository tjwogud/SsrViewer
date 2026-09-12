namespace SsrViewer
{
    internal partial class SsrWindow
    {
        const string vertexSource = """
        #version 330 core

        layout (location = 0) in vec2 aPos;
        layout (location = 1) in vec2 aUV;
        layout (location = 2) in vec4 aCol;

        uniform mat4 projection;

        out vec2 vUV;
        out vec4 vCol;

        void main()
        {
            gl_Position = projection * vec4(aPos, 0.0, 1.0);
            vUV = aUV;
            vCol = aCol;
        }
        """;

        const string fragmentSource = """
        #version 330 core

        in vec2 vUV;
        in vec4 vCol;

        uniform sampler2D tex;

        out vec4 FragColor;

        void main()
        {
            vec4 color = texture(tex, vUV) * vCol;
            color.rgb *= color.a;
            FragColor = color;
        }
        """;
    }
}
