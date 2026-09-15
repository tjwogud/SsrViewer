using OpenTK.Graphics.OpenGL4;
using Spine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;

namespace SsrViewer
{
    internal partial class SsrWindow
    {
        private void CreateBuffers()
        {
            vao = GL.GenVertexArray();
            vbo = GL.GenBuffer();
            ebo = GL.GenBuffer();

            GL.BindVertexArray(vao);

            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);

            GL.BufferData(
                BufferTarget.ArrayBuffer,
                0, IntPtr.Zero,
                BufferUsageHint.DynamicDraw
            );

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);

            GL.BufferData(
                BufferTarget.ElementArrayBuffer,
                0, IntPtr.Zero,
                BufferUsageHint.DynamicDraw
            );

            GL.VertexAttribPointer(
                0, 2,
                VertexAttribPointerType.Float,
                false,
                8 * sizeof(float), 0
            );

            GL.EnableVertexAttribArray(0);

            GL.VertexAttribPointer(
                1, 2,
                VertexAttribPointerType.Float,
                false,
                8 * sizeof(float), 2 * sizeof(float)
            );

            GL.EnableVertexAttribArray(1);

            GL.VertexAttribPointer(
                2, 4,
                VertexAttribPointerType.Float,
                false,
                8 * sizeof(float), 4 * sizeof(float)
            );

            GL.EnableVertexAttribArray(2);

            GL.BindVertexArray(0);
        }

        private void CreateRenderer()
        {
            int vertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vertexShader, vertexSource);
            GL.CompileShader(vertexShader);

            int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fragmentShader, fragmentSource);
            GL.CompileShader(fragmentShader);

            shaderProgram = GL.CreateProgram();

            GL.AttachShader(shaderProgram, vertexShader);
            GL.AttachShader(shaderProgram, fragmentShader);

            GL.LinkProgram(shaderProgram);

            GL.DeleteShader(vertexShader);
            GL.DeleteShader(fragmentShader);

            projectionLocation = GL.GetUniformLocation(shaderProgram, "projection");

            textureLocation = GL.GetUniformLocation(shaderProgram, "tex");

            GL.UseProgram(shaderProgram);
            GL.Uniform1(textureLocation, 0);
        }

        private void LoadSkeleton()
        {
            var atlas = new Atlas(atlasPath, new GLTextureLoader());

            var skeletonBinary = new SkeletonBinary(atlas);

            var skeletonData = skeletonBinary.ReadSkeletonData(skelPath);

            skeleton = new Skeleton(skeletonData);
            animationState = new AnimationState(new AnimationStateData(skeletonData));

            specialAnimAvailable = skeleton.Data.Animations.Any(anim => anim.Name == "Special");

            animationState.SetAnimation(0, "Relax", true);
            animationState.Update(0);
            animationState.Apply(skeleton);

            skeleton.UpdateWorldTransform();

            animationState.Data.DefaultMix = 0.1f;
        }

        private void CreateFramebuffer(int width, int height)
        {
            if (fbo != 0)
                DestroyFramebuffer();

            fbo = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);

            fboTexture = GL.GenTexture();

            GL.BindTexture(TextureTarget.Texture2D, fboTexture);

            GL.TexImage2D(
                TextureTarget.Texture2D,
                0,
                PixelInternalFormat.Rgba8,
                width, height, 0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                IntPtr.Zero
            );

            GL.TexParameter(
                TextureTarget.Texture2D,
                TextureParameterName.TextureMinFilter,
                (int)TextureMinFilter.Linear
            );

            GL.TexParameter(
                TextureTarget.Texture2D,
                TextureParameterName.TextureMagFilter,
                (int)TextureMagFilter.Linear
            );

            GL.FramebufferTexture2D(
                FramebufferTarget.Framebuffer,
                FramebufferAttachment.ColorAttachment0,
                TextureTarget.Texture2D,
                fboTexture,
                0
            );

            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);

            fboDepth = GL.GenRenderbuffer();

            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, fboDepth);

            GL.RenderbufferStorage(
                RenderbufferTarget.Renderbuffer,
                RenderbufferStorage.DepthComponent24,
                width, height
            );

            GL.FramebufferRenderbuffer(
                FramebufferTarget.Framebuffer,
                FramebufferAttachment.DepthAttachment,
                RenderbufferTarget.Renderbuffer,
                fboDepth
            );

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        private void DestroyFramebuffer()
        {
            if (fboTexture != 0)
                GL.DeleteTexture(fboTexture);

            if (fboDepth != 0)
                GL.DeleteRenderbuffer(fboDepth);

            if (fbo != 0)
                GL.DeleteFramebuffer(fbo);

            fbo = 0;
            fboTexture = 0;
            fboDepth = 0;
        }

        private static readonly Dictionary<int, string> voiceId = new()
        {
            { 33, "Spawn" },
            { 34, "Interact" },
            { 36, "Special" },
            { 42, "Greet" }
        };

        private const string lang = "jp";

        private void LoadVoices()
        {
            if (voiceDir == null) return;
            foreach (string voicePath in Directory.GetFiles(Path.Combine(voiceDir, lang), "*.wav"))
            {
                int id = int.Parse(Path.GetFileNameWithoutExtension(voicePath)[3..]);
                if (voiceId.TryGetValue(id, out string? strId))
                {
                    var player = new SoundPlayer(voicePath);
                    voices[strId] = player;
                }
            }
        }

        private void CreateWhiteTexture()
        {
            whiteTexture = GL.GenTexture();

            GL.BindTexture(TextureTarget.Texture2D, whiteTexture);

            byte[] whitePixel = [ 255, 255, 255, 255 ];

            GL.TexImage2D(
                TextureTarget.Texture2D,
                0,
                PixelInternalFormat.Rgba,
                1, 1, 0,
                PixelFormat.Rgba, PixelType.UnsignedByte,
                whitePixel
            );
            
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            GL.BindTexture(TextureTarget.Texture2D, 0);
        }
    }
}
