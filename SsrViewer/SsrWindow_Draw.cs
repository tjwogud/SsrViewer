using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using Spine;
using System;

namespace SsrViewer
{
    internal partial class SsrWindow
    {
        private void ApplyBlendMode(BlendMode blendMode)
        {
            switch (blendMode)
            {
                case BlendMode.Normal:
                    GL.BlendFuncSeparate(BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha,
                                         BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha);
                    break;
                case BlendMode.Additive:
                    GL.BlendFuncSeparate(BlendingFactorSrc.One, BlendingFactorDest.One,
                                         BlendingFactorSrc.One, BlendingFactorDest.One);
                    break;
                case BlendMode.Multiply:
                    GL.BlendFuncSeparate(BlendingFactorSrc.DstColor, BlendingFactorDest.OneMinusSrcAlpha,
                                         BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha);
                    break;
                case BlendMode.Screen:
                    GL.BlendFuncSeparate(BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcColor,
                                         BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha);
                    break;
            }
        }

        protected override void OnRenderFrame(FrameEventArgs args)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

            GL.BindVertexArray(vao);

            GL.UseProgram(shaderProgram);
            GL.UniformMatrix4(projectionLocation, false, ref projection);

            var clipper = new SkeletonClipping();

            foreach (Slot slot in skeleton.DrawOrder)
            {
                if (slot.Bone.Active == false)
                    continue;

                ApplyBlendMode(slot.Data.BlendMode);

                if (slot.Attachment is ClippingAttachment clip)
                {
                    clipper.ClipStart(slot, clip);
                }
                else if (slot.Attachment is RegionAttachment || slot.Attachment is MeshAttachment)
                {
                    DrawInfo info;
                    if (slot.Attachment is RegionAttachment region)
                    {
                        info = DrawRegionAttachment(slot, region);
                    }
                    else if (slot.Attachment is MeshAttachment mesh)
                    {
                        info = DrawMeshAttachment(slot, mesh);
                    }
                    else throw new Exception("wtf");
                    if (clipper.IsClipping)
                    {
                        clipper.ClipTriangles(
                            info.vertices, info.vertices.Length,
                            info.triangles, info.triangles.Length,
                            info.uv
                        );
                        info = new(
                            clipper.ClippedVertices.Count / 2,
                            [.. clipper.ClippedVertices],
                            [.. clipper.ClippedUVs],
                            info.rgba,
                            [.. clipper.ClippedTriangles],
                            info.texture
                        );
                    }

                    DrawAttachment(info);
                }

                clipper.ClipEnd(slot);
            }

            clipper.ClipEnd();

            if (!moving && stopwatch.Elapsed.TotalSeconds >= specialGuageTime)
            {
                float progress = MathF.Min(1, (float)(stopwatch.Elapsed.TotalSeconds - specialGuageTime) / (specialTime - specialGuageTime));
                var mouse = new Vector2(MousePosition.X - Size.X / 2, Size.Y * 4 / 5f - MousePosition.Y);
                DrawDonutArc(mouse.X, mouse.Y, 22, 30, 1, new(0.5f, 0.5f, 0.5f, 1));
                Vector4 color = progress == 1 ? new(0.3f, 0.85f, 0.3f, 1) : new(0.8f, 0.8f, 0.8f, 1);
                DrawDonutArc(mouse.X, mouse.Y, 22, 30, progress, color);
            }

            UpdateLayeredWindow();

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        private struct DrawInfo(int vertexCount, float[] vertices, float[] uv, float[] rgba, int[] triangles, int texture)
        {
            public int vertexCount = vertexCount;
            public float[] vertices = vertices, uv = uv, rgba = rgba;
            public int[] triangles = triangles;
            public int texture = texture;
        }

        private DrawInfo DrawRegionAttachment(Slot slot, RegionAttachment attachment)
        {
            float[] worldVertices = new float[8];

            attachment.ComputeWorldVertices(slot.Bone, worldVertices, 0);

            var uv = attachment.UVs;

            float[] rgba =
            [
                skeleton.R * slot.R * attachment.R,
                skeleton.G * slot.G * attachment.G,
                skeleton.B * slot.B * attachment.B,
                skeleton.A * slot.A * attachment.A
            ];

            var vertices = new float[2 * 4];
            for (int i = 0; i < 4; i++)
            {
                vertices[2 * i + 0] = worldVertices[2 * i + 0];
                vertices[2 * i + 1] = worldVertices[2 * i + 1];
            }

            int[] triangles =
            [
                0, 1, 2,
                2, 3, 0
            ];

            int texture = (int)((AtlasRegion)attachment.RendererObject).page.rendererObject;

            return new(4, vertices, uv, rgba, triangles, texture);
        }

        private DrawInfo DrawMeshAttachment(Slot slot, MeshAttachment attachment)
        {
            int vertexCount = attachment.WorldVerticesLength / 2;
            float[] worldVertices = new float[attachment.WorldVerticesLength];

            attachment.ComputeWorldVertices(slot, worldVertices);

            var uv = attachment.UVs;

            float[] rgba =
            [
                skeleton.R * slot.R * attachment.R,
                skeleton.G * slot.G * attachment.G,
                skeleton.B * slot.B * attachment.B,
                skeleton.A * slot.A * attachment.A
            ];

            var vertices = new float[2 * vertexCount];
            for (int i = 0; i < vertexCount; i++)
            {
                vertices[2 * i + 0] = worldVertices[2 * i + 0];
                vertices[2 * i + 1] = worldVertices[2 * i + 1];
            }

            int[] triangles = attachment.Triangles;

            int texture = (int)((AtlasRegion)attachment.RendererObject).page.rendererObject;

            return new(vertexCount, vertices, uv, rgba, triangles, texture);
        }

        private void DrawAttachment(DrawInfo info)
        {
            float[] gpuVertices = new float[8 * info.vertexCount];

            for (int i = 0; i < info.vertexCount; i++)
            {
                gpuVertices[8 * i + 0] = info.vertices[2 * i + 0];
                gpuVertices[8 * i + 1] = info.vertices[2 * i + 1];
                gpuVertices[8 * i + 2] = info.uv[2 * i + 0];
                gpuVertices[8 * i + 3] = info.uv[2 * i + 1];
                for (int j = 0; j < 4; j++)
                    gpuVertices[8 * i + 4 + j] = info.rgba[j];
            }

            GL.BindVertexArray(vao);

            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);

            GL.BufferData(
                BufferTarget.ArrayBuffer,
                gpuVertices.Length * sizeof(float), gpuVertices,
                BufferUsageHint.DynamicDraw
            );

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);

            GL.BufferData(
                BufferTarget.ElementArrayBuffer,
                info.triangles.Length * sizeof(int), info.triangles,
                BufferUsageHint.DynamicDraw
            );

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, info.texture);

            GL.DrawElements(
                PrimitiveType.Triangles,
                info.triangles.Length,
                DrawElementsType.UnsignedInt,
                0
            );
        }

        private static (float[] vertices, uint[] indices) CreateDonutArc(
            float centerX, float centerY,
            float innerRadius, float outerRadius,
            float progress, Vector4 color, int maxSegments = 96)
        {
            progress = Math.Clamp(progress, 0f, 1f);

            if (progress <= 0f)
                return (Array.Empty<float>(), Array.Empty<uint>());

            int segments = Math.Max(1, (int)MathF.Ceiling(maxSegments * progress));

            float[] vertices = new float[(segments + 1) * 2 * 8];
            uint[] indices = new uint[segments * 6];

            float totalAngle = MathF.Tau * progress;

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;

                float angle = MathF.PI * 0.5f - totalAngle * t;

                float cos = MathF.Cos(angle);
                float sin = MathF.Sin(angle);

                int outerOffset = (i * 2 + 0) * 8;
                int innerOffset = (i * 2 + 1) * 8;

                vertices[outerOffset + 0] = centerX + cos * outerRadius;
                vertices[outerOffset + 1] = centerY + sin * outerRadius;
                vertices[outerOffset + 2] = 0f;
                vertices[outerOffset + 3] = 0f;
                vertices[outerOffset + 4] = color.X;
                vertices[outerOffset + 5] = color.Y;
                vertices[outerOffset + 6] = color.Z;
                vertices[outerOffset + 7] = color.W;

                vertices[innerOffset + 0] = centerX + cos * innerRadius;
                vertices[innerOffset + 1] = centerY + sin * innerRadius;
                vertices[innerOffset + 2] = 0f;
                vertices[innerOffset + 3] = 0f;
                vertices[innerOffset + 4] = color.X;
                vertices[innerOffset + 5] = color.Y;
                vertices[innerOffset + 6] = color.Z;
                vertices[innerOffset + 7] = color.W;
            }

            for (int i = 0; i < segments; i++)
            {
                uint outer0 = (uint)(i * 2 + 0);
                uint inner0 = (uint)(i * 2 + 1);
                uint outer1 = (uint)((i + 1) * 2 + 0);
                uint inner1 = (uint)((i + 1) * 2 + 1);

                int indexOffset = i * 6;

                indices[indexOffset + 0] = outer0;
                indices[indexOffset + 1] = inner0;
                indices[indexOffset + 2] = outer1;

                indices[indexOffset + 3] = outer1;
                indices[indexOffset + 4] = inner0;
                indices[indexOffset + 5] = inner1;
            }

            return (vertices, indices);
        }

        private void DrawDonutArc(float x, float y, float innerRadius, float outerRadius, float progress, Vector4 color)
        {
            var (vertices, indices) = CreateDonutArc(
                x, y,
                innerRadius, outerRadius,
                progress,
                color
            );

            if (indices.Length == 0)
                return;

            GL.BindVertexArray(vao);

            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);

            GL.BufferData(
                BufferTarget.ArrayBuffer,
                vertices.Length * sizeof(float), vertices,
                BufferUsageHint.DynamicDraw
            );

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);

            GL.BufferData(
                BufferTarget.ElementArrayBuffer,
                indices.Length * sizeof(int), indices,
                BufferUsageHint.DynamicDraw
            );

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, whiteTexture);

            GL.DrawElements(
                PrimitiveType.Triangles,
                indices.Length,
                DrawElementsType.UnsignedInt,
                0
            );
        }
    }
}
