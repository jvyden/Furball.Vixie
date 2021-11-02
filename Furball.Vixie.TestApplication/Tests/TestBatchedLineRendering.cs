using System;
using System.Drawing;
using System.Globalization;
using System.Numerics;
using Furball.Vixie.Graphics;
using Furball.Vixie.ImGuiHelpers;
using ImGuiNET;
using Silk.NET.OpenGL.Extensions.ImGui;

namespace Furball.Vixie.TestApplication.Tests {
    public class TestBatchedLineRendering : GameComponent {
        private BatchedLineRenderer _batchedLineRenderer;

        private ImGuiController _imGuiController;

        public TestBatchedLineRendering() {}

        public override void Initialize() {
            this._batchedLineRenderer = new BatchedLineRenderer();

            this._imGuiController = ImGuiCreator.CreateController();

            base.Initialize();
        }

        private int CirnoDons = 128;

        public override void Draw(double deltaTime) {
            this.GraphicsDevice.GlClear();

            this._batchedLineRenderer.Begin();

            for (int i = 0; i != 1280; i++) {
                this._batchedLineRenderer.Draw(new Vector2(i, 0), new Vector2(1280 - i, 720), 4, Color.White);
            }

            this._batchedLineRenderer.End();



            #region ImGui menu

            this._imGuiController.Update((float) deltaTime);

            ImGui.Text($"Frametime: {Math.Round(1000.0f / ImGui.GetIO().Framerate, 2).ToString(CultureInfo.InvariantCulture)} " +
                       $"Framerate: {Math.Round(ImGui.GetIO().Framerate,           2).ToString(CultureInfo.InvariantCulture)}"
            );

            if (ImGui.Button("Go back to test selector")) {
                this.BaseGame.Components.Add(new BaseTestSelector());
                this.BaseGame.Components.Remove(this);
            }

            this._imGuiController.Render();

            #endregion

            base.Draw(deltaTime);
        }
    }
}