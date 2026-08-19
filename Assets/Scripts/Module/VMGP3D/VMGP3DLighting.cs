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
using Nofun.Driver.Unity.Graphics;
using Nofun.Util;
using Nofun.Util.Logging;
using Nofun.VM;
using System;
using UnityEngine;
using Logger = Nofun.Util.Logging.Logger;

namespace Nofun.Module.VMGP3D
{
    [Module]
    public partial class VMGP3D
    {
        private SColor fogColour;

        // Module-side copy of the active lights, kept for vLightPoint software
        // lighting (the graphic driver does not expose its light state back).
        private MpLight?[] trackedLights;

        private MpLight?[] TrackedLights => trackedLights ??= new MpLight?[system.GraphicDriver.MaxLights];

        [ModuleCall]
        private void vSetMaterial2(VMPtr<NativeMaterial2> materialPtr)
        {
            NativeMaterial2 materialCopy = materialPtr.Read(system.Memory);

            system.GraphicDriver.Material = new MpExtendedMaterial()
            {
                ambient = materialCopy.ambient.ToSColor(),
                diffuse = materialCopy.diffuse.ToSColor(),
                specular = materialCopy.specular.ToSColor(),
                emission = materialCopy.emission.ToSColor(),
                shininess = FixedUtil.FixedToFloat(materialCopy.fixedShininess)
            };
        }

        [ModuleCall]
        private void vSetMaterial(VMPtr<NativeMaterial> materialPtr)
        {
            NativeMaterial materialLegacyCopy = materialPtr.Read(system.Memory);

            system.GraphicDriver.Material = new MpExtendedMaterial()
            {
                ambient = new SColor(1.0f, 1.0f, 1.0f, 1.0f),
                diffuse = materialLegacyCopy.diffuse.ToSColor(),
                specular = materialLegacyCopy.specular.ToSColor(),
                emission = new SColor(0.0f, 0.0f, 0.0f, 0.0f),
                shininess = 4.0f
            };
        }

        [ModuleCall]
        private void vResetLights()
        {
            system.GraphicDriver.ClearLights();
            System.Array.Clear(TrackedLights, 0, TrackedLights.Length);
        }

        [ModuleCall]
        private void vSetFogColor(uint colour)
        {
            fogColour = SColor.FromRgb888(colour);
        }

        [ModuleCall]
        private void vSetLight(int index, VMPtr<NativeLight> lightPtr)
        {
            if (index < 0 || index >= system.GraphicDriver.MaxLights)
            {
                Logger.Error(LogClass.VMGP3D, $"Invalid light index: {index}");
                return;
            }

            NativeLight lightCopy = lightPtr.Read(system.Memory);
            MpLightSourceType sourceType = (MpLightSourceType)lightCopy.type;

            if ((sourceType != MpLightSourceType.Point) && (sourceType != MpLightSourceType.Spot) && (sourceType != MpLightSourceType.Directional))
            {
                Logger.Error(LogClass.VMGP3D, $"Invalid light source type: {sourceType}");
                return;
            }

            MpLight lightDriver = new MpLight()
            {
                pos = lightCopy.pos,
                dir = lightCopy.dir,
                lightSourceType = sourceType,
                diffuse = new SColor(lightCopy.r / 255.0f, lightCopy.g / 255.0f, lightCopy.b / 255.0f),
                specular = new SColor(lightCopy.sr / 255.0f, lightCopy.sg / 255.0f, lightCopy.sb / 255.0f),
                lightRange = FixedUtil.FixedToFloat(lightCopy.fixedRange),
                exponent = lightCopy.exponent,
                cutoff = (float)(FixedUtil.Fixed11PointToFloat((short)lightCopy.cutoff) * MathUtil.FullCircleRads)
            };

            if (!system.GraphicDriver.SetLight(index, lightDriver))
            {
                Logger.Error(LogClass.VMGP3D, $"Failed to set light {index}");
                return;
            }

            TrackedLights[index] = lightDriver;
        }

        [ModuleCall]
        private void vSetCameraPos(VMPtr<NativeVector3D> position)
        {
            if (!position.IsNull)
            {
                system.GraphicDriver.CameraPosition = position.Read(system.Memory);
            }
        }

        [ModuleCall]
        private void vLightPoint(VMPtr<NativeLightArgs> argsPtr)
        {
            if (argsPtr.IsNull)
            {
                return;
            }

            NativeLightArgs args = argsPtr.Read(system.Memory);

            if ((args.count == 0) || args.vertices.IsNull || args.normals.IsNull ||
                args.destDiffuses.IsNull || args.destSpeculars.IsNull)
            {
                return;
            }

            Span<NativeVector3D> vertices = args.vertices.AsSpan(system.Memory, args.count);
            Span<NativeVector3D> normals = args.normals.AsSpan(system.Memory, args.count);
            Span<NativeDiffuseColor> destDiffuses = args.destDiffuses.AsSpan(system.Memory, args.count);
            Span<NativeSpecularColor> destSpeculars = args.destSpeculars.AsSpan(system.Memory, args.count);

            bool hasVertexColors = !args.colors.IsNull;
            Span<NativeDiffuseColor> vertexColors = hasVertexColors ?
                args.colors.AsSpan(system.Memory, args.count) : default;

            MpExtendedMaterial material = system.GraphicDriver.Material;
            SColor globalAmbient = system.GraphicDriver.GlobalAmbient;
            bool specularEnabled = system.GraphicDriver.Specular;
            Vector3 cameraPos = system.GraphicDriver.CameraPosition.ToUnity();

            for (int i = 0; i < args.count; i++)
            {
                Vector3 position = vertices[i].ToUnity();
                Vector3 normal = normals[i].ToUnity().normalized;

                Color reflectance = hasVertexColors ? vertexColors[i].ToUnity() : material.diffuse.ToUnityColor();
                Color ambientReflectance = hasVertexColors ? reflectance : material.ambient.ToUnityColor();

                Color diffuse = ambientReflectance * globalAmbient.ToUnityColor() + material.emission.ToUnityColor();
                Color specular = Color.black;

                foreach (MpLight? trackedLight in TrackedLights)
                {
                    if (trackedLight == null)
                    {
                        continue;
                    }

                    MpLight light = trackedLight.Value;

                    Vector3 towardsLight;
                    float attenuation = 1.0f;

                    if (light.lightSourceType == MpLightSourceType.Directional)
                    {
                        towardsLight = -light.dir.ToUnity().normalized;
                    }
                    else
                    {
                        Vector3 delta = light.pos.ToUnity() - position;
                        float distance = delta.magnitude;

                        towardsLight = (distance > 0.0f) ? (delta / distance) : Vector3.up;

                        if (light.lightRange > 0.0f)
                        {
                            attenuation = Mathf.Clamp01(1.0f - distance / light.lightRange);
                        }
                    }

                    float diffuseFactor = Mathf.Max(0.0f, Vector3.Dot(normal, towardsLight)) * attenuation;

                    if (diffuseFactor > 0.0f)
                    {
                        diffuse += light.diffuse.ToUnityColor() * reflectance * diffuseFactor;

                        if (specularEnabled && (material.shininess > 0.0f))
                        {
                            Vector3 towardsCamera = (cameraPos - position).normalized;
                            Vector3 halfVector = (towardsLight + towardsCamera).normalized;

                            float specularFactor = Mathf.Pow(Mathf.Max(0.0f, Vector3.Dot(normal, halfVector)),
                                material.shininess) * attenuation;

                            specular += light.specular.ToUnityColor() * material.specular.ToUnityColor() * specularFactor;
                        }
                    }
                }

                destDiffuses[i] = new NativeDiffuseColor()
                {
                    r = (byte)(Mathf.Clamp01(diffuse.r) * 255.0f),
                    g = (byte)(Mathf.Clamp01(diffuse.g) * 255.0f),
                    b = (byte)(Mathf.Clamp01(diffuse.b) * 255.0f),
                    a = hasVertexColors ? vertexColors[i].a : (byte)(Mathf.Clamp01(material.diffuse.a) * 255.0f)
                };

                destSpeculars[i] = new NativeSpecularColor()
                {
                    r = (byte)(Mathf.Clamp01(specular.r) * 255.0f),
                    g = (byte)(Mathf.Clamp01(specular.g) * 255.0f),
                    b = (byte)(Mathf.Clamp01(specular.b) * 255.0f),
                    f = 0
                };
            }
        }

        [ModuleCall]
        private void vSetAmbientLight(uint colour)
        {
            system.GraphicDriver.GlobalAmbient = SColor.FromRgb888(colour);
        }
    }
}