﻿using System;
using System.Diagnostics;
using Furball.Vixie.Backends.Shared.Backends;
using Furball.Vixie.Helpers;
using JetBrains.Annotations;
using Kettu;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace Furball.Vixie; 

public abstract class Game : IDisposable {
    /// <summary>
    /// Window Input Context
    /// </summary>
    internal IInputContext InputContext;

    /// <summary>
    /// Is the Window Active/Focused?
    /// </summary>
    public bool IsActive { get; private set; }
    /// <summary>
    /// Window Manager, handles everything Window Related, from Creation to the Window Projection Matrix
    /// </summary>
    public WindowManager WindowManager { get; internal set;}
    /// <summary>
    /// All of the Game Components
    /// </summary>
    public GameComponentCollection Components { get; internal set;}

    public event EventHandler WindowRecreation;
    
    private bool _doDisplayLoadingScreen;

    private bool _recreateQueued;
    private bool _isRecreated = false;
    
    /// <summary>
    /// Creates a Game Window using `options`
    /// </summary>
    protected Game() {
        if (Global.AlreadyInitialized)
            throw new Exception("no we dont support multiple game instances yet");

        Global.AlreadyInitialized = true;
    }

    private void RunInternal(WindowOptions options, Backend backend, EventLoop eventLoop, bool requestViewOnly) {
        this.EventLoop           = eventLoop;
        this._eventLoopToChangeTo = eventLoop;
        
        try {
            Backends.Shared.Global.LatestSupportedGl = OpenGLDetector.OpenGLDetector.GetLatestSupported();
        }
        catch {
            //if an error occurs detecting the OpenGL version, fallback to legacy gl
            //todo make this logic smarter
            Backends.Shared.Global.LatestSupportedGl.GL   = new APIVersion(2, 0);
            Backends.Shared.Global.LatestSupportedGl.GLES = new APIVersion(0, 0);
        }

        if (backend == Backend.None)
            backend = GraphicsBackend.GetReccomendedBackend();

        this.WindowManager = new WindowManager(options, backend);
        if(this.EventLoop is ViewEventLoop viewEventLoop) {
            viewEventLoop.WindowManager = this.WindowManager;
            
            this.WindowManager.RequestViewOnly = requestViewOnly;

            this.WindowManager.Initialize();

            this.WindowManager.Create();
        }

        this.RehookEvents();
            
        Global.GameInstance = this;

        this.Components = new GameComponentCollection();

        Logger.AddLogger(new ConsoleLogger());
            
        Logger.StartLogging();
            
        this.EventLoop.Run();
        
        while(this._isRecreated)
            this.EventLoop.Run();
    }
    
    public void RecreateWindow([CanBeNull] EventLoop loop = null) {
        this._recreateQueued = true;
        
        if(loop != null)
            this._eventLoopToChangeTo = loop;
    }

    private void RehookEvents() {
        this.EventLoop.Start   += this.RendererInitialize;
        this.EventLoop.Closing += this.RendererOnClosing;
        this.EventLoop.Update  += this.VixieUpdate;
        this.EventLoop.Draw    += this.VixieDraw;

        if(this.EventLoop is ViewEventLoop) {
            this.WindowManager.GameView.FocusChanged      += this.EngineOnFocusChanged;
            this.WindowManager.GameView.FramebufferResize += this.EngineFrameBufferResize;
            this.WindowManager.GameView.Resize            += this.EngineViewResize;

            if (!this.WindowManager.ViewOnly) {
                this.WindowManager.GameWindow.FileDrop     += this.OnFileDrop;
                this.WindowManager.GameWindow.Move         += this.OnViewMove;
                this.WindowManager.GameWindow.StateChanged += this.EngineOnViewStateChange;
            }
        }
    }
    
    private void UnhookEvents() {
        this.EventLoop.Start   -= this.RendererInitialize;
        this.EventLoop.Closing -= this.RendererOnClosing;
        this.EventLoop.Update  -= this.VixieUpdate;
        this.EventLoop.Draw    -= this.VixieDraw;

        if(this.EventLoop is ViewEventLoop) {
            this.WindowManager.GameView.FocusChanged      -= this.EngineOnFocusChanged;
            this.WindowManager.GameView.FramebufferResize -= this.EngineFrameBufferResize;
            this.WindowManager.GameView.Resize            -= this.EngineViewResize;

            if (!this.WindowManager.ViewOnly) {
                this.WindowManager.GameWindow.FileDrop     -= this.OnFileDrop;
                this.WindowManager.GameWindow.Move         -= this.OnViewMove;
                this.WindowManager.GameWindow.StateChanged -= this.EngineOnViewStateChange;
            }
        }
    }

    protected virtual void OnWindowRecreation() {}
    
    protected void RunViewOnly(WindowOptions options, Backend backend = Backend.None) {
        this.RunInternal(options, backend, new ViewEventLoop(), true);
    }
        
    /// <summary>
    /// Runs the Game
    /// </summary>
    protected void Run(WindowOptions options, Backend backend = Backend.None) {
        this.RunInternal(options, backend, new ViewEventLoop(), false);
    }

    protected void RunHeadless() {
        //TODO: dont always choose `Dummy` backend, Vulkan can work without a window, this may be useful for CI/Automated testing
        this.RunInternal(WindowOptions.Default, Backend.Dummy, new HeadlessEventLoop(), false);
    }

    #region Renderer Actions
    /// <summary>
    /// Used to Initialize the Renderer and stuff,
    /// </summary>
    private void RendererInitialize(object sender, EventArgs eventArgs) {
        if(this.EventLoop is ViewEventLoop) {
            this.InputContext = this.WindowManager.GameView.CreateInput();

            this.WindowManager.InputContext = this.InputContext;
            this.WindowManager.SetupGraphicsApi();

            Global.WindowManager = this.WindowManager;
        }
        else {
            //todo: support other backends headless
            GraphicsBackend.SetBackend(Backend.Dummy);
            Global.GameInstance.SetApiFeatureLevels();
            GraphicsBackend.Current.SetMainThread();
            GraphicsBackend.Current.Initialize(null, null);

            GraphicsBackend.Current.HandleFramebufferResize(1280, 720);
        }

        if(!this._isRecreated) {
            this._doDisplayLoadingScreen = true;
            this.EventLoop.DoDraw();
        }

        if(!this._isRecreated)
            this.Initialize();

        if (this._isRecreated) {
            //TODO: live backend switching for new renderer
            // foreach (WeakReference<Renderer> rendererRef in Global.TRACKED_RENDERERS) {
            //     if (rendererRef.TryGetTarget(out Renderer renderer)) {
            //         renderer.Recreate();
            //         GC.ReRegisterForFinalize(renderer);
            //     }
            // }
            
            foreach (WeakReference<Texture> texRef in Global.TrackedTextures) {
                if (texRef.TryGetTarget(out Texture tex)) {
                    tex.LoadDataFromCpuToNewTexture();
                    GC.ReRegisterForFinalize(tex);
                }
            }
            
            foreach (WeakReference<RenderTarget> texRef in Global.TrackedRenderTargets) {
                if (texRef.TryGetTarget(out RenderTarget target)) {
                    target.LoadDataFromCpuToNewTexture();
                    GC.ReRegisterForFinalize(target);
                }
            }
            
            this.WindowRecreation?.Invoke(this, EventArgs.Empty);
            this.OnWindowRecreation();
        }

        this._isRecreated = false;
    }

    /// <summary>
    /// Gets Fired when the Window Gets Closed
    /// </summary>
    private void RendererOnClosing(object sender, EventArgs eventArgs) {
        this.OnClosing();
    }
    /// <summary>
    /// Gets fired when the Window Focus changes
    /// </summary>
    /// <param name="focused">New Focus</param>
    private void EngineOnFocusChanged(bool focused) {
        this.IsActive = focused;

        this.OnFocusChanged(this.IsActive);
    }
    /// <summary>
    /// Gets fired when the Window Gets Maxi/Minimized
    /// </summary>
    /// <param name="newState"></param>
    private void EngineOnViewStateChange(WindowState newState) {
        this.WindowManager.WindowState = newState;

        this.OnWindowStateChange(newState);
    }
    /// <summary>
    /// Gets Fired when The Window gets Resized
    /// </summary>
    /// <param name="newSize"></param>
    private void EngineViewResize(Vector2D<int> newSize) {
        this.OnWindowResize(newSize);
    }
    /// <summary>
    /// Gets Fired when the Frame Buffer needs/gets resized
    /// </summary>
    /// <param name="newSize">New Size</param>
    private void EngineFrameBufferResize(Vector2D<int> newSize) {
        GraphicsBackend.Current.HandleFramebufferResize(newSize.X, newSize.Y);

        this.OnFrameBufferResize(newSize);
    }

    #endregion

    #region Overrides

    /// <summary>
    /// Used to Initialize any Game Stuff before the Game Begins
    /// </summary>
    protected virtual void Initialize() {
        this.LoadContent();

        this._stopwatch.Start();
    }
    /// <summary>
    /// Used to Preload content
    /// </summary>
    protected virtual void LoadContent() {}
    /// <summary>
    /// Used to set the feature levels of all the APIs you are using
    /// </summary>
    public virtual void SetApiFeatureLevels() {}
    private double _trackedDelta = 0;
    private void VixieUpdate(object sender, double deltaTime) {
        if (this._recreateQueued) {
            // if(this.EventLoop is HeadlessEventLoop)
                // Guard.Fail("Window recreation is not save on the headless event loop");
            
            this._recreateQueued = false;
            
            //Unhook the closing event
            this.UnhookEvents();
            this.EventLoop = this._eventLoopToChangeTo;
            
            this._isRecreated = true;

            // foreach (WeakReference<Renderer> rendererRef in Global.TRACKED_RENDERERS) {
            //     if (rendererRef.TryGetTarget(out Renderer renderer)) {
            //         renderer.DisposeInternal();
            //         GC.SuppressFinalize(renderer);
            //     }
            // }
            
            foreach (WeakReference<Texture> texRef in Global.TrackedTextures) {
                if (texRef.TryGetTarget(out Texture tex)) {
                    tex.SaveDataToCpu();
                    tex.DisposeInternal();
                    GC.SuppressFinalize(tex);
                }
            }
            
            foreach (WeakReference<RenderTarget> targetRef in Global.TrackedRenderTargets) {
                if (targetRef.TryGetTarget(out RenderTarget target)) {
                    target.SaveDataToCpu();
                    target.DisposeInternal();
                    GC.SuppressFinalize(target);
                }
            }
            
            GraphicsBackend.Current.Cleanup();
            this.WindowManager.Close();
        
            this.WindowManager.Create();
            this.RehookEvents();

            return;
        }
        
        this.Update(deltaTime);
    }
    
    /// <summary>
    /// Update Method, Do your Updating work in here
    /// </summary>
    /// <param name="deltaTime">Delta Time</param>
    protected virtual void Update(double deltaTime) {
        this.Components.Update(deltaTime);

        DisposeQueue.DoDispose();

        this.CheckForInvalidTrackerResourceReferences(deltaTime);
    }
    
    [Conditional("DEBUG")]
    private void CheckForInvalidTrackerResourceReferences(double deltaTime) {
        this._trackedDelta += deltaTime;
        if (this._trackedDelta > 5) {
            Global.TrackedTextures.RemoveAll(x => !x.TryGetTarget(out _));
            Global.TrackedRenderTargets.RemoveAll(x => !x.TryGetTarget(out _));

            this._trackedDelta = 0;
        }
    }

    private readonly Stopwatch _stopwatch = new();

    public  EventLoop EventLoop;
    private EventLoop _eventLoopToChangeTo;
#if USE_IMGUI
    private          bool      _isFirstImguiUpdate = true;
    private          double    _lastImguiDrawTime;
#endif

    /// <summary>
    /// Sets up and ends the scene
    /// </summary>
    /// <param name="_"></param>
    /// <param name="deltaTime">Delta time</param>
    private void VixieDraw(object _, double deltaTime) {
        GraphicsBackend.Current.BeginScene();

        if (this._doDisplayLoadingScreen) {
            this.DrawLoadingScreen();
            this._doDisplayLoadingScreen = false;
        } else {
            this.PreDraw(deltaTime);
            this.Draw(deltaTime);
            this.PostDraw(deltaTime);
        }
#if USE_IMGUI
        if (!this._isFirstImguiUpdate)
            GraphicsBackend.Current.ImGuiDraw(deltaTime);

        double finalDelta = this._lastImguiDrawTime == 0 ? deltaTime
                                : this._stopwatch.Elapsed.TotalSeconds - this._lastImguiDrawTime;

        GraphicsBackend.Current.ImGuiUpdate(finalDelta);
        this._lastImguiDrawTime  = this._stopwatch.Elapsed.TotalSeconds;
        this._isFirstImguiUpdate = false;
#endif

        GraphicsBackend.Current.EndScene();

        GraphicsBackend.Current.Present();
            
        GraphicsBackend.Current.SetFullScissorRect();
    }
    protected virtual void PreDraw(double deltaTime) {}
    protected virtual void PostDraw(double deltaTime) {}
    /// <summary>
    /// Draw Method, do your Drawing work in there
    /// </summary>
    /// <param name="deltaTime"></param>
    protected virtual void Draw(double deltaTime) {
        this.Components.Draw(deltaTime);
    }
    protected virtual void DrawLoadingScreen() {}
    /// <summary>
    /// Dispose any IDisposables and other things left to clean up here
    /// </summary>
    public virtual void Dispose() {
        this.Components.Dispose();
        this._stopwatch.Stop();
        DisposeQueue.DisposeAll();
    }
    /// <summary>
    /// Gets fired when The Window is being closed
    /// </summary>
    protected virtual void OnClosing() {}
    /// <summary>
    /// Gets fired when a File Drag and Drop Occurs
    /// </summary>
    /// <param name="files">File paths to the Dropped files</param>
    protected virtual void OnFileDrop(string[] files) {}
    /// <summary>
    /// Gets fired when the Window Moves
    /// </summary>
    /// <param name="newPosition">New Window Position</param>
    protected virtual void OnViewMove(Vector2D<int> newPosition) {}
    /// <summary>
    /// Gets fired when the Focus of the Window Changes
    /// </summary>
    /// <param name="newFocus"></param>
    protected virtual void OnFocusChanged(bool newFocus) {}
    /// <summary>
    /// Gets Fired when The Window gets Resized
    /// </summary>
    /// <param name="newSize"></param>
    protected virtual void OnWindowResize(Vector2D<int> newSize) {}
    /// <summary>
    /// Gets Fired when the Frame Buffer needs/gets resized
    /// </summary>
    /// <param name="newSize">New Size</param>
    protected virtual void OnFrameBufferResize(Vector2D<int> newSize) {}
    /// <summary>
    /// Gets fired when the Window Gets Maximized or Minimized
    /// </summary>
    /// <param name="newState"></param>
    protected virtual void OnWindowStateChange(WindowState newState) {}

    #endregion
}