using Hexa.NET.ImGui;
using Silk.NET.Windowing;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using HexaD3D11 = Hexa.NET.ImGui.Backends.D3D11;

namespace DX11RayTrace
{
    public static class GUI
    {
        public static void DrawGUI(IWindow window, double deltaSeconds)
        {
            var io = ImGui.GetIO();
            var size = window.Size;
            var fbSize = window.FramebufferSize;
            io.DisplaySize = new Vector2(size.X, size.Y);
            io.DisplayFramebufferScale = new Vector2(fbSize.X / (float)size.X, fbSize.Y / (float)size.Y);
            io.DeltaTime = (float)deltaSeconds;
            HexaD3D11.ImGuiImplD3D11.NewFrame();
            ImGui.NewFrame();

            var displaySize = io.DisplaySize;
            float panelWidth = displaySize.X * 0.2f;

            ImGui.SetNextWindowPos(new Vector2(displaySize.X - panelWidth, 0), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(panelWidth, displaySize.Y), ImGuiCond.Always);

            ImGui.Begin("Ray Trace Settings", ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse);
            ImGui.Text("Render Data: " + Environment.NewLine + $"Width: {(int)(window.Size.X * 0.8f)} " + Environment.NewLine +$"Height: {window.Size.Y}");
            ImGui.End();

            ImGui.Render();

            HexaD3D11.ImGuiImplD3D11.RenderDrawData(ImGui.GetDrawData());
        }
    }
}
