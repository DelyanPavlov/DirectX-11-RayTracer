
using DX11RayTrace;
using Silk.NET.Direct3D.Compilers;
using Silk.NET.Direct3D11;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using static DX11RayTrace.Renderer;

ShaderParams shaderDat = new ShaderParams();

float screenW = 800, screenH = 450;
float viewPortH = 2.0f;
float viewPortW = viewPortH * ((float)screenW / screenH);

Vector3 ViewPortU = new Vector3(viewPortW, 0, 0);
Vector3 ViewPortV = new Vector3(0, -viewPortH, 0);

shaderDat.pixelDeltaU = ViewPortU / screenW;
shaderDat.pixelDeltaV = ViewPortV / screenH;
float focalLentgth = 1.0f;

Vector3 viewPortTopLeft = new Vector3(0 ,0, 0) - new Vector3(0, 0, focalLentgth) - ViewPortU / 2 - ViewPortV / 2;
shaderDat.Pixel00 = viewPortTopLeft + 0.5f * (shaderDat.pixelDeltaU + shaderDat.pixelDeltaV);

Renderer.initWindow((int)screenW, (int)screenH, shaderDat);

