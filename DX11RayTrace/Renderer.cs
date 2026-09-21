using Hexa.NET.ImGui;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D.Compilers;
using Silk.NET.Direct3D11;
using Silk.NET.DXGI;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using HexaD3D11 = Hexa.NET.ImGui.Backends.D3D11;

namespace DX11RayTrace;

public static class Renderer
{
    //public void InitWinow()
    //{
    public static Vector4 backgroundColour = new Vector4 (0.0f, 0.0f, 0.0f, 1.0f );


    [StructLayout(LayoutKind.Sequential)]
    struct ScreenParams
    {
        public Vector2 screenSize;
        public Vector2 Padding;
    }

    static float[] vertices =
    {
            //X   Y      Z
             0.6f,  1f,  0.0f,
             0.6f, -1f,  0.0f,
            -1f, -1f,  0.0f,
            -1f,  1f,  0.0f,
        };

    static uint[] indices =
    {
            0, 1, 3,
            1, 2, 3,
        };

    static uint vertexStride = 3U * sizeof(float);
    static ScreenParams frameData = new ScreenParams();
    static uint vertexOffset = 0U;

    static string PShaderSource = File.ReadAllText(@"../../../shaders/frag.hlsl");
    static string VShaderSource = File.ReadAllText(@"../../../shaders/vert.hlsl");

    // Create a window.
    static WindowOptions options = WindowOptions.Default;
    static IWindow window = Window.Create(options);
    static DXGI dxgi = null!;
    static D3D11 d3d11 = null!;
    static D3DCompiler compiler = null!;

    // Thee variables are initialized within the Load event.
    static ComPtr<IDXGIFactory2> factory = default;
    static ComPtr<IDXGISwapChain1> swapchain = default;
    static ComPtr<ID3D11Device> device = default;
    static ComPtr<ID3D11DeviceContext> deviceContext = default;
    static ComPtr<ID3D11Buffer> vertexBuffer = default;
    static ComPtr<ID3D11Buffer> indexBuffer = default;
    static ComPtr<ID3D11Buffer> constantBuffer = default;
    static ComPtr<ID3D11VertexShader> vertexShader = default;
    static ComPtr<ID3D11PixelShader> pixelShader = default;
    static ComPtr<ID3D11InputLayout> inputLayout = default;


    public static void initWindow( int screenW, int ScreenH)
    {
        options.Size = new Vector2D<int>(screenW, ScreenH);
        options.Title = "RaytTracer(DX11)";
        options.API = GraphicsAPI.None; // <-- This bit is important, as your window will be configured for OpenGL by default
        // Assign events.
        window.Load += OnLoad;
        window.Update += OnUpdate;
        window.Render += OnRender;
        window.FramebufferResize += OnFramebufferResize;

        // Run the window.
        window.Run();

        // ImGui must be shut down BEFORE the device it renders with is released.
        HexaD3D11.ImGuiImplD3D11.Shutdown();
        ImGui.DestroyContext();

        // Clean up any resources. 
        factory.Dispose();
        swapchain.Dispose();
        device.Dispose();
        deviceContext.Dispose();
        vertexBuffer.Dispose();
        indexBuffer.Dispose();
        constantBuffer.Dispose();
        vertexShader.Dispose();
        pixelShader.Dispose();
        inputLayout.Dispose();
        compiler.Dispose();
        d3d11.Dispose();
        dxgi.Dispose();

        //dispose the window, and its internal resources
        window.Dispose();
    }
    
    unsafe static void OnLoad()
    {
        //Whether or not to force use of DXVK on platforms where native DirectX implementations are available
        const bool forceDxvk = false;

        dxgi = DXGI.GetApi(window, forceDxvk);
        d3d11 = D3D11.GetApi(window, forceDxvk);
        compiler = D3DCompiler.GetApi();

        // Set-up input context.
        var input = window.CreateInput();
        foreach (var keyboard in input.Keyboards)
        {
            keyboard.KeyDown += OnKeyDown;
            keyboard.KeyDown += (_, key, _) => ImGuiKeyEvent(key, true);
            keyboard.KeyUp += (_, key, _) => ImGuiKeyEvent(key, false);
            keyboard.KeyChar += (_, c) => ImGui.GetIO().AddInputCharacter(c);
        }

        foreach (var mouse in input.Mice)
        {
            mouse.MouseMove += (_, pos) => ImGui.GetIO().AddMousePosEvent(pos.X, pos.Y);
            mouse.MouseDown += (_, button) =>
            {
                if ((int)button <= 2) ImGui.GetIO().AddMouseButtonEvent((int)button, true);
            };
            mouse.MouseUp += (_, button) =>
            {
                if ((int)button <= 2) ImGui.GetIO().AddMouseButtonEvent((int)button, false);
            };
            mouse.Scroll += (_, wheel) => ImGui.GetIO().AddMouseWheelEvent(wheel.X, wheel.Y);
        }

        // Create our D3D11 logical device.
        SilkMarshal.ThrowHResult
        (
            d3d11.CreateDevice
            (
                default(ComPtr<IDXGIAdapter>),
                D3DDriverType.Hardware,
                Software: default,
                (uint)CreateDeviceFlag.Debug,
                null,
                0,
                D3D11.SdkVersion,
                ref device,
                null,
                ref deviceContext
            )
        );

        //This is not supported under DXVK 
        //TODO: PR a stub into DXVK for this maybe?
        if (OperatingSystem.IsWindows())
        {
            // Log debug messages for this device (given that we've enabled the debug flag). Don't do this in release code!
            device.SetInfoQueueCallback(msg => Console.WriteLine(SilkMarshal.PtrToString((nint)msg.PDescription)));
        }

        // Setup ImGui (needs the device + context, so it lives after CreateDevice).
        var imguiContext = ImGui.CreateContext();
        ImGui.SetCurrentContext(imguiContext);
        ImGui.StyleColorsDark();
        ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;

        // Hexa's backends are separate native libraries, so they need to be told which ImGui context to use.
        HexaD3D11.ImGuiImplD3D11.SetCurrentContext(ImGui.GetCurrentContext());
        HexaD3D11.ImGuiImplD3D11.Init
        (
            (HexaD3D11.ID3D11Device*)device.Handle,
            (HexaD3D11.ID3D11DeviceContext*)deviceContext.Handle
        );

        // Create our swapchain.
        var swapChainDesc = new SwapChainDesc1
        {
            BufferCount = 2, // double buffered
            Format = Format.FormatB8G8R8A8Unorm,
            BufferUsage = DXGI.UsageRenderTargetOutput,
            SwapEffect = SwapEffect.FlipDiscard,
            SampleDesc = new SampleDesc(1, 0)
        };

        // Create our DXGI factory to allow us to create a swapchain. 
        factory = dxgi.CreateDXGIFactory<IDXGIFactory2>();

        // Create the swapchain.
        SilkMarshal.ThrowHResult
        (
            factory.CreateSwapChainForHwnd
            (
                device,
                window.Native!.DXHandle!.Value,
                in swapChainDesc,
                null,
                ref Unsafe.NullRef<IDXGIOutput>(),
                ref swapchain
            )
        );

        // Create our vertex buffer.
        var bufferDesc = new BufferDesc
        {
            ByteWidth = (uint)(vertices.Length * sizeof(float)),
            Usage = Usage.Default,
            BindFlags = (uint)BindFlag.VertexBuffer
        };

        fixed (float* vertexData = vertices)
        {
            var subresourceData = new SubresourceData
            {
                PSysMem = vertexData
            };

            SilkMarshal.ThrowHResult(device.CreateBuffer(in bufferDesc, in subresourceData, ref vertexBuffer));
        }

        // Create our index buffer.
        bufferDesc = new BufferDesc
        {
            ByteWidth = (uint)(indices.Length * sizeof(uint)),
            Usage = Usage.Default,
            BindFlags = (uint)BindFlag.IndexBuffer
        };

        fixed (uint* indexData = indices)
        {
            var subresourceData = new SubresourceData
            {
                PSysMem = indexData
            };

            SilkMarshal.ThrowHResult(device.CreateBuffer(in bufferDesc, in subresourceData, ref indexBuffer));
        }


        //Create screen data buffer
        var SDbufferDat = new BufferDesc
        {
            ByteWidth = (uint)sizeof(ScreenParams),
            Usage = Usage.Dynamic,                         // updated every frame from the CPU
            BindFlags = (uint)BindFlag.ConstantBuffer,
            CPUAccessFlags = (uint)CpuAccessFlag.Write
        };
        SilkMarshal.ThrowHResult(device.CreateBuffer(in SDbufferDat, in Unsafe.NullRef<SubresourceData>(), ref constantBuffer));

        var shaderBytes = Encoding.ASCII.GetBytes(VShaderSource);

        // Compile vertex shader.
        ComPtr<ID3D10Blob> vertexCode = default;
        ComPtr<ID3D10Blob> vertexErrors = default;
        HResult hr = compiler.Compile
        (
            in shaderBytes[0],
            (nuint)shaderBytes.Length,
            nameof(VShaderSource),
            null,
            ref Unsafe.NullRef<ID3DInclude>(),
            "vs_main",
            "vs_5_0",
            0,
            0,
            ref vertexCode,
            ref vertexErrors
        );

        // Check for compilation errors.
        if (hr.IsFailure)
        {
            if (vertexErrors.Handle is not null)
            {
                Console.WriteLine(SilkMarshal.PtrToString((nint)vertexErrors.GetBufferPointer()));
            }

            hr.Throw();
        }

        shaderBytes = Encoding.ASCII.GetBytes(PShaderSource);
        // Compile pixel shader.
        ComPtr<ID3D10Blob> pixelCode = default;
        ComPtr<ID3D10Blob> pixelErrors = default;
        hr = compiler.Compile
        (
            in shaderBytes[0],
            (nuint)shaderBytes.Length,
            nameof(PShaderSource),
            null,
            ref Unsafe.NullRef<ID3DInclude>(),
            "ps_main",
            "ps_5_0",
            0,
            0,
            ref pixelCode,
            ref pixelErrors
        );

        // Check for compilation errors.
        if (hr.IsFailure)
        {
            if (pixelErrors.Handle is not null)
            {
                Console.WriteLine(SilkMarshal.PtrToString((nint)pixelErrors.GetBufferPointer()));
            }

            hr.Throw();
        }

        // Create vertex shader.
        SilkMarshal.ThrowHResult
        (
            device.CreateVertexShader
            (
                vertexCode.GetBufferPointer(),
                vertexCode.GetBufferSize(),
                ref Unsafe.NullRef<ID3D11ClassLinkage>(),
                ref vertexShader
            )
        );

        // Create pixel shader.
        SilkMarshal.ThrowHResult
        (
            device.CreatePixelShader
            (
                pixelCode.GetBufferPointer(),
                pixelCode.GetBufferSize(),
                ref Unsafe.NullRef<ID3D11ClassLinkage>(),
                ref pixelShader
            )
        );

        // Describe the layout of the input data for the shader.
        fixed (byte* name = SilkMarshal.StringToMemory("POS"))
        {
            var inputElement = new InputElementDesc
            {
                SemanticName = name,
                SemanticIndex = 0,
                Format = Format.FormatR32G32B32Float,
                InputSlot = 0,
                AlignedByteOffset = 0,
                InputSlotClass = InputClassification.PerVertexData,
                InstanceDataStepRate = 0
            };

            SilkMarshal.ThrowHResult
            (
                device.CreateInputLayout
                (
                    in inputElement,
                    1,
                    vertexCode.GetBufferPointer(),
                    vertexCode.GetBufferSize(),
                    ref inputLayout
                )
            );
        }

        // Clean up any resources.
        vertexCode.Dispose();
        vertexErrors.Dispose();
        pixelCode.Dispose();
        pixelErrors.Dispose();
    }


    static void OnUpdate(double deltaSeconds)
    {
        // Here all of the updates to program state ahead of rendering (e.g. physics) should be done. We don't have anything
        // to do here at the moment, so we've left it blank.
    }

    unsafe static void OnFramebufferResize(Vector2D<int> newSize)
    {
        // If the window resizes, we need to be sure to update the swapchain's back buffers.
        // (The render target view is created and released every frame in OnRender, so nothing else holds the buffers.)
        SilkMarshal.ThrowHResult
        (
            swapchain.ResizeBuffers(0, (uint)newSize.X, (uint)newSize.Y, Format.FormatB8G8R8A8Unorm, 0)
        );
    }

    unsafe static void OnRender(double deltaSeconds)
    {

        // Obtain the framebuffer for the swapchain's backbuffer.
        using var framebuffer = swapchain.GetBuffer<ID3D11Texture2D>(0);

        // Create a view over the render target.
        ComPtr<ID3D11RenderTargetView> renderTargetView = default;
        SilkMarshal.ThrowHResult(device.CreateRenderTargetView(framebuffer, null, ref renderTargetView));

        // Clear the render target to be all black ahead of rendering.
        deviceContext.ClearRenderTargetView(renderTargetView, ref backgroundColour.X);

        // Update the rasterizer state with the current viewport.
        var viewport = new Viewport(0, 0, window.FramebufferSize.X, window.FramebufferSize.Y, 0, 1);
        deviceContext.RSSetViewports(1, in viewport);

        // Tell the output merger about our render target view.
        deviceContext.OMSetRenderTargets(1, ref renderTargetView, ref Unsafe.NullRef<ID3D11DepthStencilView>());

        // Update the input assembler to use our shader input layout, and associated vertex & index buffers.
        deviceContext.IASetPrimitiveTopology(D3DPrimitiveTopology.D3DPrimitiveTopologyTrianglelist);
        deviceContext.IASetInputLayout(inputLayout);
        deviceContext.IASetVertexBuffers(0, 1, vertexBuffer, in vertexStride, in vertexOffset);
        deviceContext.IASetIndexBuffer(indexBuffer, Format.FormatR32Uint, 0);

        // Bind our shaders.
        deviceContext.VSSetShader(vertexShader, ref Unsafe.NullRef<ComPtr<ID3D11ClassInstance>>(), 0);
        deviceContext.PSSetShader(pixelShader, ref Unsafe.NullRef<ComPtr<ID3D11ClassInstance>>(), 0);

        frameData.screenSize = new Vector2(window.Size.X * 0.8f, window.Size.Y);

        MappedSubresource mapped = default;
        SilkMarshal.ThrowHResult(deviceContext.Map(constantBuffer, 0, Map.WriteDiscard, 0, ref mapped));
        *(ScreenParams*)mapped.PData = frameData;
        deviceContext.Unmap(constantBuffer, 0);

        deviceContext.PSSetConstantBuffers(0, 1, ref constantBuffer);

        // Draw the quad.
        deviceContext.DrawIndexed(6, 0, 0);

        GUI.DrawGUI(window, deltaSeconds);

        // Present the drawn image.
        swapchain.Present(1, 0);

        // Clean up any resources created in this method.
        renderTargetView.Dispose();
    }

    static void OnKeyDown(IKeyboard keyboard, Key key, int scancode)
    {
        // Check to close the window on escape.
        if (key == Key.Escape)
        {
            window.Close();
        }
    }

    static void ImGuiKeyEvent(Key key, bool down)
    {
        var imguiKey = ToImGuiKey(key);
        if (imguiKey != ImGuiKey.None)
        {
            ImGui.GetIO().AddKeyEvent(imguiKey, down);
        }
    }

    static ImGuiKey ToImGuiKey(Key key)
    {
        if (key >= Key.A && key <= Key.Z)
        {
            return ImGuiKey.A + (key - Key.A);
        }

        return key switch
        {
            Key.Tab => ImGuiKey.Tab,
            Key.Left => ImGuiKey.LeftArrow,
            Key.Right => ImGuiKey.RightArrow,
            Key.Up => ImGuiKey.UpArrow,
            Key.Down => ImGuiKey.DownArrow,
            Key.PageUp => ImGuiKey.PageUp,
            Key.PageDown => ImGuiKey.PageDown,
            Key.Home => ImGuiKey.Home,
            Key.End => ImGuiKey.End,
            Key.Insert => ImGuiKey.Insert,
            Key.Delete => ImGuiKey.Delete,
            Key.Backspace => ImGuiKey.Backspace,
            Key.Space => ImGuiKey.Space,
            Key.Enter => ImGuiKey.Enter,
            Key.Escape => ImGuiKey.Escape,
            Key.ShiftLeft => ImGuiKey.LeftShift,
            Key.ShiftRight => ImGuiKey.RightShift,
            Key.ControlLeft => ImGuiKey.LeftCtrl,
            Key.ControlRight => ImGuiKey.RightCtrl,
            Key.AltLeft => ImGuiKey.LeftAlt,
            Key.AltRight => ImGuiKey.RightAlt,
            _ => ImGuiKey.None,
        };
    }
}
