﻿using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Blish_HUD.Controls.Extern;
using Blish_HUD.Controls.Intern;
using Blish_HUD.GameIntegration;
using Blish_HUD.Settings;
using Microsoft.Xna.Framework;

namespace Blish_HUD {

    public class GameIntegrationService : GameService {

        private static readonly Logger Logger = Logger.GetLogger<GameIntegrationService>();

        private const string GAMEINTEGRATION_SETTINGS = "GameIntegrationConfiguration";

        /// <summary>
        /// Contains information and references about the attached Guild Wars 2 process.
        /// </summary>
        public Gw2InstanceIntegration Gw2Instance { get; private set; }

        /// <summary>
        /// Contains information pulled from the attached Guild Wars 2's in-game graphics settings (via GSA API file).
        /// </summary>
        public GfxSettingsIntegration GfxSettings { get; private set; }

        /// <summary>
        /// Contains information about the attached Guild Wars 2's client type.
        /// </summary>
        public ClientTypeIntegration ClientType { get; private set; }

        /// <summary>
        /// Contains information about any running TacO processes.
        /// </summary>
        public TacOIntegration TacO { get; private set; }

        /// <summary>
        /// Contains our own WinForm integrations.
        /// </summary>
        public WinFormsIntegration WinForms { get; private set; }

        #region Obsolete Gw2Instance

        private void WireOldEvents() {
#pragma warning disable 0612, 0618
            this.Gw2Instance.Gw2Closed        += (sender, e) => this.Gw2Closed?.Invoke(sender, e);
            this.Gw2Instance.Gw2Started       += (sender, e) => this.Gw2Started?.Invoke(sender, e);
            this.Gw2Instance.Gw2AcquiredFocus += (sender, e) => this.Gw2AcquiredFocus?.Invoke(sender, e);
            this.Gw2Instance.Gw2LostFocus     += (sender, e) => this.Gw2LostFocus?.Invoke(sender, e);
            this.Gw2Instance.IsInGameChanged  += (sender, e) => this.IsInGameChanged?.Invoke(sender, e);
#pragma warning restore 0612, 0618
        }

        [Obsolete("Use GameIntegration.Gw2Instance.Gw2Closed (0.11.0+) instead.")]
        public event EventHandler<EventArgs> Gw2Closed;

        [Obsolete("Use GameIntegration.Gw2Instance.Gw2Started (0.11.0+) instead.")]
        public event EventHandler<EventArgs> Gw2Started;


        [Obsolete("Use GameIntegration.Gw2Instance.Gw2AcquiredFocus (0.11.0+) instead.")]
        public event EventHandler<EventArgs> Gw2AcquiredFocus;

        [Obsolete("Use GameIntegration.Gw2Instance.Gw2LostFocus (0.11.0+) instead.")]
        public event EventHandler<EventArgs> Gw2LostFocus;

        [Obsolete("Use GameIntegration.Gw2Instance.IsInGameChanged (0.11.0+) instead.")]
        public event EventHandler<ValueEventArgs<bool>> IsInGameChanged;
        
        public IGameChat Chat { get; private set; }

        [Obsolete("Use GameIntegration.Gw2Instance.IsInGame (0.11.0+) instead.")]
        public bool IsInGame => this.Gw2Instance.IsInGame;

        [Obsolete("Use GameIntegration.Gw2Instance.Gw2HasFocus (0.11.0+) instead.")]
        public bool Gw2HasFocus => this.Gw2Instance.Gw2HasFocus;

        [Obsolete("Use GameIntegration.Gw2Instance.Gw2IsRunning (0.11.0+) instead.")]
        public bool Gw2IsRunning => this.Gw2Instance.Gw2IsRunning;

        [Obsolete("Use GameIntegration.Gw2Instance.Gw2WindowHandle (0.11.0+) instead.")]
        public IntPtr Gw2WindowHandle => this.Gw2Instance.Gw2WindowHandle;

        [Obsolete("Use GameIntegration.Gw2Instance.Gw2ExecutablePath (0.11.0+) instead.")]
        public string Gw2ExecutablePath => this.Gw2Instance.Gw2ExecutablePath;

        [Obsolete("Use GameIntegration.Gw2Instance.Gw2Process (0.11.0+) instead.")]
        public Process Gw2Process => this.Gw2Instance.Gw2Process;

        [Obsolete("Use GameIntegration.Gw2Instance.FocusGw2() (0.11.0+) instead.")]
        public void FocusGw2() => this.Gw2Instance.FocusGw2();

        #endregion

        internal SettingCollection ServiceSettings { get; private set; }

        internal GameIntegrationService() {
            SetServiceModules(this.Gw2Instance = new Gw2InstanceIntegration(this),
                              this.GfxSettings = new GfxSettingsIntegration(this),
                              this.ClientType  = new ClientTypeIntegration(this),
                              this.TacO        = new TacOIntegration(this),
                              this.WinForms    = new WinFormsIntegration(this));
        }

        protected override void Initialize() {
            this.ServiceSettings = Settings.RegisterRootSettingCollection(GAMEINTEGRATION_SETTINGS);

            Chat = new GameChat();
        }

        protected override void Load() {
            BlishHud.Instance.Form.Shown += delegate {
                WindowUtil.SetupOverlay(BlishHud.Instance.FormHandle);
            };

            WireOldEvents();
        }

        protected override void Unload() { /* NOOP */ }

        protected override void Update(GameTime gameTime) { /* NOOP */ }

        #region Chat Interactions
        /// <summary>
        /// Methods related to interaction with the in-game chat.
        /// </summary>
        public interface IGameChat {
            /// <summary>
            /// Sends a message to the chat.
            /// </summary>
            void Send(string message);
            /// <summary>
            /// Adds a string to the input field.
            /// </summary>
            void Paste(string text);
            /// <summary>
            /// Returns the current string in the input field.
            /// </summary>
            Task<string> GetInputText();
            /// <summary>
            /// Clears the input field.
            /// </summary>
            void Clear();
        }
        ///<inheritdoc/>
        private class GameChat : IGameChat {
            ///<inheritdoc/>
            [Obsolete("No longer supported here in Core.", true)]
            public async void Send(string message) {
                if (IsBusy() || !IsTextValid(message)) return;
                byte[] prevClipboardContent = await ClipboardUtil.WindowsClipboardService.GetAsUnicodeBytesAsync();
                await ClipboardUtil.WindowsClipboardService.SetTextAsync(message)
                                   .ContinueWith(clipboardResult => {
                                       if (clipboardResult.IsFaulted)
                                           Logger.Warn(clipboardResult.Exception, "Failed to set clipboard text to {message}!", message);
                                       else
                                           Task.Run(() => {
                                               Focus();
                                               Keyboard.Press(VirtualKeyShort.LCONTROL, true);
                                               Keyboard.Stroke(VirtualKeyShort.KEY_V, true);
                                               Thread.Sleep(50);
                                               Keyboard.Release(VirtualKeyShort.LCONTROL, true);
                                               Keyboard.Stroke(VirtualKeyShort.RETURN);
                                           }).ContinueWith(result => {
                                               if (result.IsFaulted) {
                                                   Logger.Warn(result.Exception, "Failed to send message {message}", message);
                                               } else if (prevClipboardContent != null)
                                                   ClipboardUtil.WindowsClipboardService.SetUnicodeBytesAsync(prevClipboardContent);
                                           }); });
            }

            ///<inheritdoc/>
            [Obsolete("No longer supported here in Core.", true)]
            public async void Paste(string text) {
                if (IsBusy()) return;
                string currentInput = await GetInputText();
                if (!IsTextValid(currentInput + text)) return;
                byte[] prevClipboardContent = await ClipboardUtil.WindowsClipboardService.GetAsUnicodeBytesAsync();
                await ClipboardUtil.WindowsClipboardService.SetTextAsync(text)
                                   .ContinueWith(clipboardResult => {
                                       if (clipboardResult.IsFaulted)
                                           Logger.Warn(clipboardResult.Exception, "Failed to set clipboard text to {text}!", text);
                                       else
                                           Task.Run(() => {
                                               Focus();
                                               Keyboard.Press(VirtualKeyShort.LCONTROL, true);
                                               Keyboard.Stroke(VirtualKeyShort.KEY_V, true);
                                               Thread.Sleep(50);
                                               Keyboard.Release(VirtualKeyShort.LCONTROL, true);
                                           }).ContinueWith(result => {
                                               if (result.IsFaulted) {
                                                   Logger.Warn(result.Exception, "Failed to paste {text}", text);
                                               } else if (prevClipboardContent != null)
                                                   ClipboardUtil.WindowsClipboardService.SetUnicodeBytesAsync(prevClipboardContent);
                                           }); });
            }

            ///<inheritdoc/>
            [Obsolete("No longer supported here in Core.", true)]
            public async Task<string> GetInputText() {
                if (IsBusy()) return "";
                byte[] prevClipboardContent = await ClipboardUtil.WindowsClipboardService.GetAsUnicodeBytesAsync();
                await Task.Run(() => {
                    Focus();
                    Keyboard.Press(VirtualKeyShort.LCONTROL, true);
                    Keyboard.Stroke(VirtualKeyShort.KEY_A, true);
                    Keyboard.Stroke(VirtualKeyShort.KEY_C, true);
                    Thread.Sleep(50);
                    Keyboard.Release(VirtualKeyShort.LCONTROL, true);
                    Unfocus();
                });
                string inputText = await ClipboardUtil.WindowsClipboardService.GetTextAsync()
                                                      .ContinueWith(result => {
                                                          if (prevClipboardContent != null)
                                                              ClipboardUtil.WindowsClipboardService.SetUnicodeBytesAsync(prevClipboardContent);
                                                          return !result.IsFaulted ? result.Result : "";
                                                      });
                return inputText;
            }
            ///<inheritdoc/>
            [Obsolete("No longer supported here in Core.", true)]
            public void Clear() {
                if (IsBusy()) return;
                Task.Run(() => {
                    Focus();
                    Keyboard.Press(VirtualKeyShort.LCONTROL, true);
                    Keyboard.Stroke(VirtualKeyShort.KEY_A, true);
                    Thread.Sleep(50);
                    Keyboard.Release(VirtualKeyShort.LCONTROL, true);
                    Keyboard.Stroke(VirtualKeyShort.BACK);
                    Unfocus();
                });
            }

            private void Focus() {
                Unfocus();
                Keyboard.Stroke(VirtualKeyShort.RETURN);
            }

            private void Unfocus() {
                Mouse.Click(MouseButton.LEFT, Graphics.WindowWidth / 2, 0);
            }

            private bool IsTextValid(string text) {
                return (text != null && text.Length < 200);
                // More checks? (Symbols: https://wiki.guildwars2.com/wiki/User:MithranArkanere/Charset)
            }
            private bool IsBusy() {
                return !GameIntegration.Gw2Instance.Gw2IsRunning || !GameIntegration.Gw2Instance.Gw2HasFocus || !GameIntegration.Gw2Instance.IsInGame;
            }
        }
        #endregion

    }
}