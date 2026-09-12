using OpenTK.Graphics.OpenGL4;
using Spine;
using System.Drawing;
using System.Drawing.Imaging;

namespace SsrViewer
{
    internal class GLTextureLoader : TextureLoader
    {
        public void Load(AtlasPage page, string path)
        {
            int texture = GL.GenTexture();

            GL.BindTexture(TextureTarget.Texture2D, texture);

            using var bitmap = new Bitmap(path);

            var data = bitmap.LockBits(
                new(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb
            );

            GL.PixelStore(PixelStoreParameter.UnpackAlignment, 1);

            GL.TexImage2D(
                TextureTarget.Texture2D,
                0,
                PixelInternalFormat.Rgba,
                bitmap.Width, bitmap.Height, 0,
                OpenTK.Graphics.OpenGL4.PixelFormat.Bgra,
                PixelType.UnsignedByte,
                data.Scan0
            );

            bitmap.UnlockBits(data);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            page.rendererObject = texture;

            page.width = bitmap.Width;
            page.height = bitmap.Height;
        }

        public void Unload(object texture)
        {
            GL.DeleteTexture((int)texture);
        }
    }
}
