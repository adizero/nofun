/*
 * (C) 2023 Radrat Softworks
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Nofun.Driver.Graphics;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nofun.Driver.Unity.Graphics
{
    public class BufferPusher
    {
        private const MeshUpdateFlags ShutUpFlags = MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds;

        private struct Vertex
        {
            public Vector3 position;
            public Vector3 normal;
            public Color color;
            public Vector2 uv;
            public Color specularColor;
        }

        private Mesh bigMesh;

        private int maxIndiciesCount;
        private int maxVerticesCount;

        private List<Vertex> vertices;
        private List<int> indicies;
        private List<SubMeshDescriptor> submeshes;

        private List<Vertex> verticesSwap;
        private List<int> indiciesSwap;
        private List<SubMeshDescriptor> submeshesSwap;

        public Mesh BigMesh => bigMesh;

        public BufferPusher(int maxVerticesCount = 10000, int maxIndiciesCount = 30000)
        {
            bigMesh = new Mesh();

            bigMesh.SetVertexBufferParams(maxVerticesCount, new VertexAttributeDescriptor[]
            {
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4)
            });

            bigMesh.SetIndexBufferParams(maxIndiciesCount, IndexFormat.UInt32);

            this.maxVerticesCount = maxVerticesCount;
            this.maxIndiciesCount = maxIndiciesCount;

            vertices = new();
            indicies = new();
            submeshes = new();

            verticesSwap = new();
            indiciesSwap = new();
            submeshesSwap = new();
        }

        public int Push(MpMesh meshes)
        {
            int totalVertices = meshes.vertices.Length;
            int totalIndicies = IndiciesTransformer.Estimate(meshes.indices, meshes.topology);

            if ((vertices.Count + totalVertices > maxVerticesCount) || (indicies.Count + totalIndicies > maxIndiciesCount))
            {
                return -1;
            }

            int vertexOffset = vertices.Count;

            for (int i = 0; i < meshes.vertices.Length; i++)
            {
                vertices.Add(new Vertex()
                {
                    position = meshes.vertices[i].ToUnity(),
                    uv = meshes.uvs.IsEmpty ? Vector2.zero : meshes.uvs[i].ToUnity(),
                    normal = meshes.normals.IsEmpty ? Vector3.one : meshes.normals[i].ToUnity(),
                    color = meshes.diffuses.IsEmpty ? Color.white : meshes.diffuses[i].ToUnity(),
                    specularColor = meshes.speculars.IsEmpty ? Color.white : meshes.speculars[i].ToUnity()
                });
            }

            int indiciesOffset = indicies.Count;

            indicies.AddRange(IndiciesTransformer.Process(meshes.indices, meshes.topology, vertexOffset));
            submeshes.Add(new SubMeshDescriptor(indiciesOffset, totalIndicies));

            return submeshes.Count - 1;
        }

        public int Push(List<Vector3> positions, List<Vector2> uvs, List<Vector3> normals, List<Color> colors, List<Color> specularColors, List<int> indiciesSpan)
        {
            if ((vertices.Count + positions.Count > maxVerticesCount) || (indicies.Count + indiciesSpan.Count > maxIndiciesCount))
            {
                return -1;
            }

            int vertexOffset = vertices.Count;

            for (int i = 0; i < positions.Count; i++)
            {
                vertices.Add(new Vertex()
                {
                    position = positions[i],
                    uv = uvs[i],
                    normal = normals[i],
                    color = colors[i],
                    specularColor = specularColors[i]
                });
            }

            int indiciesOffset = indicies.Count;

            indicies.AddRange(indiciesSpan.ToArray());

            submeshes.Add(new SubMeshDescriptor(indiciesOffset, indiciesSpan.Count)
            {
                baseVertex = vertexOffset
            });

            return submeshes.Count - 1;
        }

        public void Flush()
        {
            List<Vertex> verticesBackup = vertices;
            List<int> indiciesBackup = indicies;
            List<SubMeshDescriptor> submeshesBackup = submeshes;

            JobScheduler.Instance.PostponeToUnityThread(() =>
            {
                bigMesh.SetVertexBufferData(verticesBackup, 0, 0, verticesBackup.Count, 0, ShutUpFlags);
                bigMesh.SetIndexBufferData(indiciesBackup, 0, 0, indiciesBackup.Count, ShutUpFlags);
                bigMesh.SetSubMeshes(submeshesBackup, ShutUpFlags);

                verticesBackup.Clear();
                indiciesBackup.Clear();
                submeshesBackup.Clear();
            }, true);

            (verticesSwap, vertices) = (vertices, verticesSwap);
            (indiciesSwap, indicies) = (indicies, indiciesSwap);
            (submeshesSwap, submeshes) = (submeshes, submeshesSwap);
        }
    }
}