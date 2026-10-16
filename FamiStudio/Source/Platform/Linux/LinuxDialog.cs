using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using static GLFWDotNet.GLFW;

namespace FamiStudio
{
    public class LinuxDialog
    {
        public enum DialogMode
        {
            Open = 0,
            Save = 1,
            Folder = 2,
            Message = 3
        }

        public enum DialogBackend
        {
            None = -1,
            GTK = 0,
            Kdialog = 1,
            Zenity = 2,
        }

        private const string CanberraGtkPlay = "canberra-gtk-play";
        private const string FlatpakInfoPath = "/.flatpak-info";
        private const string FlatpakPrefix = "/app";
        private const string FlatpakIdEnvVar = "FLATPAK_ID";
        private const string DisplayEnvVar = "DISPLAY";
        private const string XdgCurrentDesktopEnvVar = "XDG_CURRENT_DESKTOP";
        private const string XdgSessionTypeEnvVar = "XDG_SESSION_TYPE";
        private const string GdkBackendEnvVar = "GDK_BACKEND";
        private const string GobjectDllName = "libgobject-2.0.so.0";
        private const string GtkDllName = "libgtk-3.so.0";
        private const string GdkDllName = "libgdk-3.so.0";
        private const int LC_ALL = 6;

        private static LinuxDialog dlgInstance;
        private static readonly DialogBackend dialogBackend = DialogBackend.None;
        private static readonly string xdgSessionType;
        private static readonly string gdkBackend;
        private static readonly bool isCanberraAvailable;
        private static readonly bool isDisplayAvailable;
        private static readonly bool isX11;
        private static readonly bool isWayland;
        private static readonly bool isGdkValid;
        private static readonly bool isRunningInFlatpak;
        private static readonly IntPtr x11DisplayHandle;

        private static bool isWaylandTransient;

        public static bool IsDialogOpen => dlgInstance != null && dialogBackend != DialogBackend.None;

        static LinuxDialog()
        {
            isDisplayAvailable = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(DisplayEnvVar));
            isRunningInFlatpak = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(FlatpakIdEnvVar));
            xdgSessionType     = Environment.GetEnvironmentVariable(XdgSessionTypeEnvVar);
            gdkBackend         = Environment.GetEnvironmentVariable(GdkBackendEnvVar);

            isX11     = xdgSessionType?.ToLowerInvariant() == "x11";
            isWayland = !isX11;

            if (isX11)
                x11DisplayHandle = glfwGetX11Display();

            // Needed for GTK to localise buttons based on the system language.
            SetLocale(LC_ALL, "");

            // Try GTK first, falling back to kdialog, and finally zenity. If all options are exhausted,
            // the user is prompted to install one of the above, or disable OS dialogs in settings.

            // NOTE: GDK_BACKEND must NOT be set to x11 when using Wayland, and vice versa. This will cause 
            // instability, such as intermittent crashing while initializing GTK or the first dialog.
            // We can workaround by skipping GTK if the backend doesn't match the session type.
            isGdkValid = gdkBackend == null || string.Equals(gdkBackend, xdgSessionType);

            if (isGdkValid && TryInitializeGtk())
            {
                dialogBackend = DialogBackend.GTK;
                isCanberraAvailable = CanberraNative.Initialize(GetGtkSoundTheme());
            }

            else if (IsCommandAvailable("kdialog"))
                dialogBackend = DialogBackend.Kdialog;

            else if (IsCommandAvailable("zenity"))
                dialogBackend = DialogBackend.Zenity;
        }

        // GTK Message Types
        private const int GTK_MESSAGE_INFO = 0;
        private const int GTK_MESSAGE_WARNING = 1;
        private const int GTK_MESSAGE_QUESTION = 2;
        private const int GTK_MESSAGE_ERROR = 3;
        private const int GTK_MESSAGE_OTHER = 4;

        // GTK Buttons
        private const int GTK_BUTTONS_NONE = 0;
        private const int GTK_BUTTONS_OK = 1;
        private const int GTK_BUTTONS_CLOSE = 2;
        private const int GTK_BUTTONS_CANCEL = 3;
        private const int GTK_BUTTONS_YES_NO = 4;
        private const int GTK_BUTTONS_OK_CANCEL = 5;

        // GTK Button Icons
        public const int GTK_ICON_SIZE_BUTTON = 1;

        // GTK Responses
        private const int GTK_RESPONSE_ACCEPT = -3;
        private const int GTK_RESPONSE_DELETE_EVENT = -4;
        private const int GTK_RESPONSE_OK = -5;
        private const int GTK_RESPONSE_CANCEL = -6;
        private const int GTK_RESPONSE_CLOSE = -7;
        private const int GTK_RESPONSE_YES = -8;
        private const int GTK_RESPONSE_NO = -9;

        private DialogMode dialogMode;
        private string dialogTitle;
        private string dialogText;
        private string dialogExts;
        private string dialogPath;
        private string flatpakPath;
        private bool dialogMulti;

        // GTK workarounds (when transient is not supported).
        private bool hasPresented;
        private bool shouldKeepAbove = true;

        private MessageBoxButtons dialogButtons;

        public string[] SelectedPaths { get; private set; }
        public DialogResult MessageBoxSelection { get; private set; }

        #region Localization

        // Used for zenity and GTK, which don't have default 3 button dialogs.
        LocalizedString CancelLabel;

        // Used if GTK, kdialog, and zenity are not found.
        LocalizedString DialogErrorTitle;
        LocalizedString DialogErrorMessage;

        #endregion

        [StructLayout(LayoutKind.Sequential)]
        private struct GValue
        {
            public UIntPtr Type;
            public GValueData Data0;
            public GValueData Data1;
        }

        [StructLayout(LayoutKind.Explicit, Size = 8)]
        private struct GValueData
        {
            [FieldOffset(0)] public IntPtr Pointer;
            [FieldOffset(0)] public double Double;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void GDestroyNotify(IntPtr data);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void GtkResponseCallback(IntPtr dialog, int response_id, IntPtr user_data);

        [DllImport(GobjectDllName, EntryPoint = "g_signal_connect_data", CallingConvention = CallingConvention.Cdecl)]
        private static extern ulong GSignalConnectData(IntPtr instance, string detailed_signal, GtkResponseCallback handler, IntPtr data, IntPtr destroy_data, int connect_flags);

        [DllImport(GobjectDllName, EntryPoint = "g_type_check_instance_is_a", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool GTypeCheckInstanceIsA(IntPtr obj, IntPtr gType);

        [DllImport(GobjectDllName, EntryPoint = "g_object_unref", CallingConvention = CallingConvention.Cdecl)]
        private static extern void GObjectUnref(IntPtr obj);

        [DllImport(GobjectDllName, EntryPoint = "g_object_get_property",CallingConvention = CallingConvention.Cdecl)]
        private static extern void GObjectGetProperty(IntPtr obj, [MarshalAs(UnmanagedType.LPUTF8Str)] string propertyName, ref GValue value);

        [DllImport(GobjectDllName, EntryPoint = "g_value_get_string", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr GValueGetString(ref GValue value);

        [DllImport(GobjectDllName, EntryPoint = "g_value_unset", CallingConvention = CallingConvention.Cdecl)]
        private static extern void GValueUnset(ref GValue value);

        [DllImport(GtkDllName, EntryPoint = "gtk_init", CallingConvention = CallingConvention.Cdecl)]
        private static extern void GtkInit(int argc, IntPtr argv);

        [DllImport(GtkDllName, EntryPoint = "gtk_window_set_title", CallingConvention = CallingConvention.Cdecl)]
        private static extern void GtkWindowSetTitle(IntPtr dialog, string title);

        [DllImport(GtkDllName, EntryPoint = "gtk_file_chooser_dialog_new", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr GtkFileChooserDialogNew(
            string title,
            IntPtr parent,
            int action,
            string firstButtonText, int firstResponse,
            string secondButtonText, int secondResponse,
            IntPtr nullTerminator);

        [DllImport(GtkDllName, EntryPoint = "gtk_message_dialog_new", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr GtkMessageDialogNew(
            IntPtr parent,
            int flags,
            int type,
            int buttons,
            string message_format);

        [DllImport(GtkDllName, EntryPoint = "gtk_dialog_add_button", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr GtkDialogAddButton(IntPtr dialog, string button_text, int response_id);

        [DllImport(GtkDllName, EntryPoint = "gtk_dialog_add_action_widget", CallingConvention = CallingConvention.Cdecl)]
        public static extern void GtkDialogAddActionWidget(IntPtr dialog, IntPtr widget, int responseId);

        [DllImport(GtkDllName, EntryPoint = "gtk_dialog_run", CallingConvention = CallingConvention.Cdecl)]
        private static extern int GtkDialogRun(IntPtr dialog);

        [DllImport(GtkDllName, EntryPoint = "gtk_widget_show", CallingConvention = CallingConvention.Cdecl)]
        private static extern void GtkWidgetShow(IntPtr widget);

        [DllImport(GtkDllName, EntryPoint = "gtk_widget_show_all", CallingConvention = CallingConvention.Cdecl)]
        private static extern void GtkWidgetShowAll(IntPtr widget);

        [DllImport(GtkDllName, EntryPoint = "gtk_widget_get_visible", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool GtkGetWidgetVisible(IntPtr widget);

        [DllImport(GtkDllName, EntryPoint = "gtk_window_get_type", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr GtkWindowGetType();

        [DllImport(GtkDllName, EntryPoint = "gtk_widget_get_type", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr GtkWidgetGetType();

        [DllImport(GtkDllName, EntryPoint = "gtk_widget_destroy", CallingConvention = CallingConvention.Cdecl)]
        private static extern void GtkWidgetDestroy(IntPtr widget);

        [DllImport(GtkDllName, EntryPoint = "gtk_dialog_get_widget_for_response", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GtkDialogGetWidgetForResponse(IntPtr dialog, int responseId);

        [DllImport(GtkDllName, EntryPoint = "gtk_widget_get_window", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr GtkWidgetGetWindow(IntPtr widget);

        [DllImport(GtkDllName, EntryPoint = "gtk_widget_realize", CallingConvention = CallingConvention.Cdecl)]
        private static extern void GtkWidgetRealize(IntPtr widget);

        [DllImport(GtkDllName, EntryPoint = "gtk_window_is_active", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool GtkWindowIsActive(IntPtr window);

        [DllImport(GtkDllName, EntryPoint = "gtk_window_set_keep_above", CallingConvention = CallingConvention.Cdecl)]
        private static extern void GtkWindowSetKeepAbove(IntPtr window, bool setting);

        [DllImport(GtkDllName, EntryPoint = "gtk_window_set_transient_for", CallingConvention = CallingConvention.Cdecl)]
        private static extern void GtkWindowSetTransientFor(IntPtr window, IntPtr parent);

        [DllImport(GtkDllName, EntryPoint = "gtk_window_present", CallingConvention = CallingConvention.Cdecl)]
        private static extern void GtkWindowPresent(IntPtr window);

        [DllImport(GtkDllName, EntryPoint = "gtk_window_set_modal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void GtkWindowSetModal(IntPtr window, bool modal);

        [DllImport(GtkDllName, EntryPoint = "gtk_window_set_icon", CallingConvention = CallingConvention.Cdecl)]
        private static extern void GtkWindowSetIcon(IntPtr window, IntPtr icon);

        [DllImport(GtkDllName, EntryPoint = "gtk_file_chooser_set_create_folders", CallingConvention = CallingConvention.Cdecl)]
        private static extern void GtkFileChooserSetCreateFolders(IntPtr chooser, bool create);

        [DllImport(GtkDllName, EntryPoint = "gtk_file_chooser_set_current_folder", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool GtkFileChooserSetCurrentFolder(IntPtr chooser, string folder);

        [DllImport(GtkDllName, EntryPoint = "gtk_file_chooser_get_filename", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr GtkFileChooserGetFilename(IntPtr dialog);

        [DllImport(GtkDllName, EntryPoint = "gtk_file_chooser_get_filenames", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr GtkFileChooserGetFilenames(IntPtr dialog);

        [DllImport(GtkDllName, EntryPoint = "gtk_file_chooser_set_select_multiple", CallingConvention = CallingConvention.Cdecl)]
        private static extern void GtkFileChooserSetSelectMultiple(IntPtr dialog, bool select_multiple);

        [DllImport(GtkDllName, EntryPoint = "gtk_file_chooser_set_do_overwrite_confirmation", CallingConvention = CallingConvention.Cdecl)]
        public static extern void GtkFileChooserSetDoOverwriteConfirmation(IntPtr chooser, bool setting);

        [DllImport(GtkDllName, EntryPoint = "gtk_file_filter_new", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr GtkFileFilterNew();

        [DllImport(GtkDllName, EntryPoint = "gtk_file_filter_set_name", CallingConvention = CallingConvention.Cdecl)]
        private static extern void GtkFileFilterSetName(IntPtr filter, string name);

        [DllImport(GtkDllName, EntryPoint = "gtk_file_filter_add_pattern", CallingConvention = CallingConvention.Cdecl)]
        private static extern void GtkFileFilterAddPattern(IntPtr filter, string pattern);

        [DllImport(GtkDllName, EntryPoint = "gtk_file_chooser_add_filter", CallingConvention = CallingConvention.Cdecl)]
        private static extern void GtkFileChooserAddFilter(IntPtr chooser, IntPtr filter);

        [DllImport(GtkDllName, EntryPoint = "gtk_file_chooser_set_filename", CallingConvention = CallingConvention.Cdecl)]
        public static extern void GtkFileChooserSetFileName(IntPtr dialog, string name);

        [DllImport(GtkDllName, EntryPoint = "gtk_file_chooser_set_current_name", CallingConvention = CallingConvention.Cdecl)]
        public static extern void GtkFileChooserSetCurrentName(IntPtr dialog, string name);

        [DllImport(GtkDllName, EntryPoint = "gtk_button_new_with_label", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GtkButtonNewWithLabel(string label);

        [DllImport(GtkDllName, EntryPoint = "gtk_button_set_image", CallingConvention = CallingConvention.Cdecl)]
        public static extern void GtkButtonSetImage(IntPtr button, IntPtr image);

        [DllImport(GtkDllName, EntryPoint = "gtk_button_set_always_show_image", CallingConvention = CallingConvention.Cdecl)]
        public static extern void GtkButtonSetAlwaysShowImage(IntPtr button, bool alwaysShow);

        [DllImport(GtkDllName, EntryPoint = "gtk_image_new_from_icon_name", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GtkImageNewFromIconName(string iconName, int size);

        [DllImport(GtkDllName, EntryPoint = "g_free", CallingConvention = CallingConvention.Cdecl)]
        private static extern void GFree(IntPtr ptr);

        [DllImport(GtkDllName, EntryPoint = "g_slist_free_full", CallingConvention = CallingConvention.Cdecl)]
        private static extern void GSlistFreeFull(IntPtr list, GDestroyNotify freeFunc);

        [DllImport(GtkDllName, EntryPoint = "gtk_events_pending", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool GtkEventsPending();

        [DllImport(GtkDllName, EntryPoint = "gtk_main_iteration", CallingConvention = CallingConvention.Cdecl)]
        private static extern void GtkMainIteration();

        [DllImport(GtkDllName, EntryPoint = "gtk_settings_get_default", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr GtkSettingsGetDefault();

        [DllImport(GdkDllName, EntryPoint = "gdk_x11_window_get_xid", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr GdkX11WindowGetXid(IntPtr gdkWindow);

        [DllImport(GdkDllName, EntryPoint = "gdk_wayland_window_set_transient_for_exported", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool GdkWaylandWindowSetTransientForExported(IntPtr gdkWindow, string parentHandleStr);

        [DllImport(GdkDllName, EntryPoint = "gdk_wayland_surface_get_wl_surface", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr GdkWaylandSurfaceGetWlSurface(IntPtr gdkWindow);

        [DllImport(GdkDllName, EntryPoint = "gdk_pixbuf_new_from_data", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr GdkPixbufNewFromData(
            IntPtr data,
            int colorspace,
            bool hasAlpha,
            int bitsPerSample,
            int width,
            int height,
            int rowstride,
            IntPtr destroy_fn,
            IntPtr destroy_fn_data);

        [DllImport("libc", EntryPoint = "setlocale", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SetLocale(int category, string locale);

        [DllImport("libX11")]
        private static extern void XSetTransientForHint(IntPtr display, IntPtr w, IntPtr parent);

        public LinuxDialog(DialogMode mode, string title, ref string defaultPath, string extensions = "", bool multiselect = false)
        {
            Debug.Assert(dlgInstance == null);

            dlgInstance = this;
            Localization.Localize(this);

            dialogMode  = mode;
            dialogTitle = title;
            dialogExts  = extensions;
            dialogMulti = multiselect;
            dialogPath  = defaultPath;

            SelectedPaths = ShowFileDialog();
            
            // Only update the default path if it's valid.
            if (dialogPath != null && Path.Exists(dialogPath))
                defaultPath = dialogPath;

            dlgInstance = null;
        }

        public LinuxDialog(string text, string title, MessageBoxButtons buttons)
        {
            Debug.Assert(dlgInstance == null);

            dlgInstance = this;
            Localization.Localize(this);

            dialogMode    = DialogMode.Message;
            dialogTitle   = title;
            dialogText    = text;
            dialogButtons = buttons;

            MessageBoxSelection = ShowMessageBoxDialog();
            dlgInstance = null;
        }

        private static bool IsGtkWindow(IntPtr ptr)
        {
            return GTypeCheckInstanceIsA(ptr, GtkWindowGetType());
        }

        private static bool IsGtkWidget(IntPtr ptr)
        {
            return GTypeCheckInstanceIsA(ptr, GtkWidgetGetType());
        }

        private static bool TryInitializeGtk()
        {
            try
            {
                GtkInit(0, IntPtr.Zero);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize GTK: {ex.Message}");
                return false;
            }
        }

        private static string GetGtkSoundTheme()
        {
            IntPtr settings = GtkSettingsGetDefault();

            if (settings == IntPtr.Zero)
                return null;

            GValue value = default;

            try
            {
                value.Type = (UIntPtr)64;
                GObjectGetProperty(settings, "gtk-sound-theme-name", ref value);

                IntPtr str = GValueGetString(ref value);
                return str == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(str);
            }
            catch
            {
                return null;
            }
            finally
            {
                if (value.Type != UIntPtr.Zero)
                    GValueUnset(ref value);
            }
        }

        private static bool IsCommandAvailable(string program)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "/bin/sh",
                    Arguments = $"-c \"command -v {program}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                process.WaitForExit();

                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
        
        private static void AddGtkButtonWithIcon(IntPtr dialog, string text, string iconName, int response)
        {
            GtkDialogAddButton(dialog, text, response);
            IntPtr button = GtkDialogGetWidgetForResponse(dialog, response);
            IntPtr icon = GtkImageNewFromIconName(iconName, GTK_ICON_SIZE_BUTTON);
            GtkButtonSetImage(button, icon);
            GtkButtonSetAlwaysShowImage(button, true);
        }

        private void ShowErrorMessage()
        {
            var dlg = new MessageDialog(FamiStudioWindow.Instance, DialogErrorMessage, DialogErrorTitle, MessageBoxButtons.OK);
            dlg.ShowDialog();
        }

        private unsafe IntPtr CreateGtkWindowIcon()
        {
            var tga = TgaFile.LoadFromResource("FamiStudio.Resources.Icons.FamiStudio_32.tga");

            var width = tga.Width;
            var height = tga.Height;
            int[] data = tga.Data;

            for (var i = 0; i < data.Length; i++)
            {
                var pixel = data[i];

                var g = (byte)(pixel >> 0);
                var b = (byte)(pixel >> 8);
                var r = (byte)(pixel >> 16);
                var a = (byte)(pixel >> 24);

                data[i] = (r << 0) | (g << 16) | (b << 8) | (a << 24);
            }

            IntPtr pixbuf;

            fixed (int* p = &data[0])
            {
                pixbuf = GdkPixbufNewFromData((IntPtr)p, 0, true, 8, width, height, width * 4, IntPtr.Zero, IntPtr.Zero);
            }

            return pixbuf;
        }

        private void ConditionalSetFlatpakPaths()
        {
            if (isRunningInFlatpak && dialogPath.StartsWith(FlatpakPrefix))
            {
                try
                {
                    if (File.Exists(FlatpakInfoPath))
                    {
                        var lines = File.ReadAllLines(FlatpakInfoPath);

                        foreach (var line in lines)
                        {
                            if (line.StartsWith("app-path="))
                            {
                                flatpakPath = line.Split('=', 2)[1].Trim(); // System flatpak path.
                                dialogPath = string.Concat(flatpakPath, dialogPath.AsSpan(FlatpakPrefix.Length));
                                break;
                            }
                        }
                    }
                }
                catch { }
            }
        }

        private void ConditionalRestoreFlatpakPaths(string[] paths)
        {
            if (isRunningInFlatpak && !string.IsNullOrEmpty(flatpakPath) && paths != null)
            {
                for (var i = 0; i < paths.Length; i++)
                    if (paths[i].StartsWith(flatpakPath))
                        paths[i] = string.Concat(FlatpakPrefix, paths[i].AsSpan(flatpakPath.Length));
            }
        }

        private void TrySimulateTransientBehaviour(IntPtr dialog)
        {
            var isFsActive  = FamiStudioWindow.Instance.IsWindowInFocus;
            var isGtkActive = GtkWindowIsActive(dialog);

            var shouldPresent = isFsActive && !hasPresented;
            var isActive      = isFsActive || isGtkActive;

            if (shouldPresent)
            {
                // Present the window if the FS window is clicked. Environments behave differently here.
                // Some bring the window back in front, others will highlight the icon on the taskbar.
                // Environments like GNOME sometimes show "x is ready" messages instead.
                GtkWindowPresent(dialog);
                GtkWindowSetKeepAbove(dialog, true);
                hasPresented = true;
            }

            // Only toggle when needed, some environments dislike multiple toggles.
            if (shouldKeepAbove != isActive)
            {
                GtkWindowSetKeepAbove(dialog, isActive);
                shouldKeepAbove = isActive;
            }

            hasPresented = isFsActive;
        }

        private string[] ShowFileDialog()
        {
            if (isDisplayAvailable)
            {
                string[] response = null;

                // If we're using flatpak and are within the sandboxed "/app" path, the
                // external process can't see it. We use the real system path to it instead.
                // NOTE: GTK can see the real "/app" path and doesn't need this.
                if (dialogBackend != DialogBackend.None)
                {
                    // Loop until a valid response is provided. The loop will exit once
                    // the response is a valid path or null. Otherwise, the dialog is
                    // presented again.
                    while (true)
                    {
                        switch (dialogBackend)
                        {
                            case DialogBackend.GTK:
                                response = ShowGtkFileDialog();
                                break;

                            case DialogBackend.Kdialog:
                                ConditionalSetFlatpakPaths();
                                response = ShowKdialogFileDialog();
                                break;

                            case DialogBackend.Zenity:
                                ConditionalSetFlatpakPaths();
                                response = ShowZenityFileDialog();
                                break;
                        }

                        if (response != null)
                        {
                            // Replace backslashes with forward slashes (Linux path separators).
                            // I can't see why anyone would intentionally put "\" in the filename.
                            for (int i = 0; i < response.Length; i++)
                            {
                                response[i] = response[i].Replace('\\', Path.DirectorySeparatorChar);
                            }

                            var responseDir = Path.GetDirectoryName(response[0]);

                            // Safety: Ensure the path exists to prevent a possible crash. This
                            // likely only applies to saving, but it's better to be safe.
                            if (Path.Exists(responseDir))
                            {
                                // Set the last known valid path. We can only read this after the
                                // dialog exits, due to it being an external process.
                                dialogPath = responseDir;

                                // When saving, we need to ensure the path has write access.
                                if (dialogMode == DialogMode.Save)
                                {
                                    try
                                    {
                                        // HACK: Create dummy file at target path to test write access.
                                        using (FileStream fs = File.Create(response[0], 0, FileOptions.DeleteOnClose)) {}

                                        return response;
                                    }
                                    catch {}
                                }
                                else
                                {
                                    return response;
                                }
                            }

                            response = null;
                        }
                        else
                        {
                            return null;
                        }

                        // If response is invalid, represent the dialog (loop continues).
                        Platform.Beep();
                    }
                }
            }

            ShowErrorMessage();
            return null;
        }

        private string[] ShowGtkFileDialog()
        {
            IntPtr dialog = GtkFileChooserDialogNew(
                dialogTitle,
                IntPtr.Zero,
                (int)dialogMode,
                "gtk-cancel", GTK_RESPONSE_CANCEL,
                dialogMode == DialogMode.Save ? "gtk-save" : "gtk-open", GTK_RESPONSE_ACCEPT,
                IntPtr.Zero);

            // Button icons.
            var cancelBtn = GtkDialogGetWidgetForResponse(dialog, GTK_RESPONSE_CANCEL);
            if (cancelBtn != IntPtr.Zero)
            {
                IntPtr cancelIcon = GtkImageNewFromIconName("process-stop", GTK_ICON_SIZE_BUTTON);
                GtkButtonSetImage(cancelBtn, cancelIcon);
                GtkButtonSetAlwaysShowImage(cancelBtn, true);
            }
            var openBtn = GtkDialogGetWidgetForResponse(dialog, GTK_RESPONSE_ACCEPT);
            if (openBtn != IntPtr.Zero)
            {
                var iconName = dialogMode == DialogMode.Save ? "document-save-symbolic" : "document-open-symbolic";
                var openIcon = GtkImageNewFromIconName(iconName, GTK_ICON_SIZE_BUTTON);
                GtkButtonSetImage(openBtn, openIcon);
                GtkButtonSetAlwaysShowImage(openBtn, true);
            }

            if (dialogMulti)
                GtkFileChooserSetSelectMultiple(dialog, true);

            GtkFileChooserSetCurrentFolder(dialog, dialogPath);

            // Extension filters
            var extPairs = dialogExts.Split("|");
            if (dialogMode != DialogMode.Folder)
            {
                for (var i = 0; i < extPairs.Length; i += 2)
                {
                    IntPtr filter = GtkFileFilterNew();

                    var pair = extPairs[i].Split('(');
                    GtkFileFilterSetName(filter, pair[0].Trim());

                    foreach (var ext in pair[1].Split(';'))
                    {
                        GtkFileFilterAddPattern(filter, ext.TrimEnd(')'));
                    }

                    GtkFileChooserAddFilter(dialog, filter);
                }
            }

            var extension = extPairs[1].Trim().Substring(1);

            if (dialogMode != DialogMode.Open)
            {
                GtkFileChooserSetCreateFolders(dialog, true);

                if (dialogMode == DialogMode.Save)
                {
                    GtkFileChooserSetDoOverwriteConfirmation(dialog, true);
                    GtkFileChooserSetCurrentName(dialog, extension);
                }
            }

            var response = ShowGtkDialog(dialog, extension);
            return response.paths;
        }

        private string[] ShowKdialogFileDialog()
        {
            var args = $"--title \"{dialogTitle}\" ";

            var extPairs = dialogExts.Split("|");
            var filters  = "";

            if (dialogMode != DialogMode.Folder)
            {
                Debug.Assert(extPairs.Length % 2 == 0);

                for (int i = 0; i < extPairs.Length - 1; i += 2)
                {
                    var name = extPairs[i].Split(" (")[0]; ;
                    var pattern = extPairs[i + 1].Replace(";", " ");
                    filters += $"{name} {pattern}|";
                }

                filters = filters.TrimEnd('|');
            }

            switch (dialogMode)
            {
                case DialogMode.Open:
                    args += $"--getopenfilename \"{dialogPath}\" \"{filters}{(dialogMulti ? " --multiple --separate-output" : "")}\"";
                    break;

                case DialogMode.Save:
                    args += $"--getsavefilename \"{dialogPath}\" \"{filters}\"";
                    break;

                case DialogMode.Folder:
                    args += $"--getexistingdirectory \"{dialogPath}\"";
                    break;
            }

            var result = ShowDialog("kdialog", args);
            return result.paths.Length > 0 ? result.paths : null;
        }

        private string[] ShowZenityFileDialog()
        {
            var filters  = "";
            var extPairs = dialogExts.Split("|");

            if (dialogMode != DialogMode.Folder)
            {
                Debug.Assert(extPairs.Length % 2 == 0);

                for (int i = 0; i < extPairs.Length - 1; i += 2)
                    filters += "--file-filter=\"" + (extPairs[i] + "|" + extPairs[i + 1]).Replace(";", " ").Trim() + "\" ";
            }

            var args = $"--file-selection --title=\"{dialogTitle}\" --filename=\"{dialogPath.TrimEnd('/')}/\" {filters} ";

            switch (dialogMode)
            {
                case DialogMode.Open:
                    if (dialogMulti) args += "--multiple --separator=\"\n\"";
                    break;

                case DialogMode.Save:
                    args += "--save";
                    break;

                case DialogMode.Folder:
                    args += "--directory";
                    break;
            }

            var result = ShowDialog("zenity", args);
            return result.paths.Length > 0 ? result.paths : null;
        }

        private DialogResult ShowMessageBoxDialog()
        {
            if (isDisplayAvailable)
            {
                // Try to play a notification sound.
                if (isCanberraAvailable)
                {
                    CanberraNative.PlaySound("dialog-warning");
                }

                switch (dialogBackend)
                {
                    case DialogBackend.GTK:     return ShowGtkMessageBoxDialog();
                    case DialogBackend.Kdialog: return ShowKdialogMessageBoxDialog();
                    case DialogBackend.Zenity:  return ShowZenityMessageBoxDialog();
                }
            }

            ShowErrorMessage();
            return DialogResult.None;
        }

        private DialogResult ShowGtkMessageBoxDialog()
        {
            var messageType = dialogButtons == MessageBoxButtons.OK ? GTK_MESSAGE_INFO : GTK_MESSAGE_QUESTION;

            IntPtr dialog = GtkMessageDialogNew(IntPtr.Zero, 0, messageType, GTK_BUTTONS_NONE, dialogText);
            GtkWindowSetTitle(dialog, dialogTitle);

            // We manually create the buttons so we can use icons.
            if (dialogButtons == MessageBoxButtons.YesNo || dialogButtons == MessageBoxButtons.YesNoCancel)
            {
                AddGtkButtonWithIcon(dialog, "gtk-yes", "emblem-ok", GTK_RESPONSE_YES);
                AddGtkButtonWithIcon(dialog, "gtk-no", "window-close-symbolic", GTK_RESPONSE_NO);
            }

            if (dialogButtons == MessageBoxButtons.YesNoCancel)
            {
                AddGtkButtonWithIcon(dialog, "gtk-cancel", "process-stop", GTK_RESPONSE_CANCEL);
            }

            if (dialogButtons == MessageBoxButtons.OK)
            {
                AddGtkButtonWithIcon(dialog, "gtk-ok", "emblem-ok", GTK_RESPONSE_OK);
            }

            // If the window failed to initialize, we are only displaying an error.
            var response = FamiStudioWindow.Instance != null
                ? ShowGtkDialog(dialog).exitCode
                : GtkDialogRun(dialog);

            DialogResult result = response switch
            {
                GTK_RESPONSE_DELETE_EVENT => DialogResult.Cancel,
                GTK_RESPONSE_OK           => DialogResult.OK,
                GTK_RESPONSE_CANCEL       => DialogResult.Cancel,
                GTK_RESPONSE_YES          => DialogResult.Yes,
                GTK_RESPONSE_NO           => DialogResult.No,
                _                         => DialogResult.None
            };

            return result;
        }

        private DialogResult ShowKdialogMessageBoxDialog()
        {
            string args = string.Empty;

            switch (dialogButtons)
            {
                case MessageBoxButtons.OK:
                    args = $"--msgbox \"{dialogText}\" --title \"{dialogTitle}\"";
                    break;

                case MessageBoxButtons.YesNo:
                    args = $"--yesno \"{dialogText}\" --title \"{dialogTitle}\"";
                    break;

                case MessageBoxButtons.YesNoCancel:
                    args = $"--yesnocancel \"{dialogText}\" --title \"{dialogTitle}\"";
                    break;
            }

            var result = ShowDialog("kdialog", args);
            var exitCode = result.exitCode;

            return exitCode switch
            {
                0 => dialogButtons == MessageBoxButtons.OK ? DialogResult.OK : DialogResult.Yes,
                1 => DialogResult.No,
                2 => DialogResult.Cancel,
                _ => DialogResult.None,
            };
        }

        private DialogResult ShowZenityMessageBoxDialog()
        {
            var args = string.Empty;
            switch (dialogButtons)
            {
                case MessageBoxButtons.OK:
                    args = $"--info --title=\"{dialogTitle}\" --text=\"{dialogText}\"";
                    break;

                case MessageBoxButtons.YesNo:
                    args = $"--question --title=\"{dialogTitle}\" --text=\"{dialogText}\"";
                    break;

                case MessageBoxButtons.YesNoCancel:
                    args = $"--question --title=\"{dialogTitle}\" --text=\"{dialogText}\" --extra-button=\"{CancelLabel}\"";
                    break;
            }

            var result   = ShowDialog("zenity", args);
            var exitCode = result.exitCode;

            if (exitCode == 0)
            {
                return dialogButtons == MessageBoxButtons.OK ? DialogResult.OK : DialogResult.Yes;
            }
            else if (exitCode == 1)
            {
                // No and Cancel are both exit code 1 on zenity, since it doesn't natively support 3 buttons. "No" returns an empty array.
                if (result.paths.Length == 0)
                {
                    return DialogResult.No;
                }
                else if (result.paths[0] == CancelLabel)
                {
                    return DialogResult.Cancel;
                }
            }

            return DialogResult.None;
        }

        private (string[] paths, int exitCode) ShowGtkDialog(IntPtr dialog, string ext = "")
        {
            var icon = CreateGtkWindowIcon();

            if (icon != IntPtr.Zero)
                GtkWindowSetIcon(dialog, icon);

            // Make dialog transient on Wayland. This is a bit more work than X11.
            // It requires getting the window handle and it may only work on certain
            // environments. Idea is to use it non-transient when it doesn't work.
            if (isGdkValid && isWayland)
            {
                GtkWidgetRealize(dialog);

                var h = WaylandForeign.Export(glfwGetWaylandDisplay(), FamiStudioWindow.Instance.Handle);
                if (h != null)
                    isWaylandTransient = GdkWaylandWindowSetTransientForExported(GtkWidgetGetWindow(dialog), h);
            }

            GtkWidgetShowAll(dialog);

            // X11 needs to set transient AFTER the dialog is created.
            if (isGdkValid && isX11)
            {
                IntPtr gtkX11Window = GdkX11WindowGetXid(GtkWidgetGetWindow(dialog));

                if (gtkX11Window != IntPtr.Zero)
                    XSetTransientForHint(x11DisplayHandle, gtkX11Window, FamiStudioWindow.Instance.Handle);
            }

            var response = -1;
            var done = false;
            string[] paths = null;

            // Use a callback and custom loop to keep the window responsive.
            GtkResponseCallback callback = (dlg, responseId, userData) =>
            {
                if (responseId == GTK_RESPONSE_ACCEPT && dialogMode == DialogMode.Save)
                {
                    IntPtr filenamePtr = GtkFileChooserGetFilename(dlg);
                    if (filenamePtr != IntPtr.Zero)
                    {
                        var path = Marshal.PtrToStringUTF8(filenamePtr);

                        if (!string.IsNullOrEmpty(ext) && !path.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                        {
                            var newName = Path.GetFileName(path).TrimEnd('.') + ext;
                            GtkFileChooserSetCurrentName(dlg, newName);
                            GFree(filenamePtr);
                            return;
                        }

                        paths = new[] { path };
                        dialogPath = Path.GetDirectoryName(path);
                        GFree(filenamePtr);
                    }
                }

                response = responseId;
                done = true;
            };

            var callbackHandle = GCHandle.Alloc(callback);

            GSignalConnectData(dialog, "response", callback, IntPtr.Zero, IntPtr.Zero, 0);
            GSignalConnectData(dialog, "delete-event", (dlg, eventPtr, userData) =>
            {
                callback(dlg, GTK_RESPONSE_DELETE_EVENT, userData);
            }, IntPtr.Zero, IntPtr.Zero, 0);

            bool alertSoundPlayed = true;

            while (!done)
            {
                while (GtkEventsPending())
                    GtkMainIteration();

                if (!IsGtkWindow(dialog))
                    break;

                FamiStudioWindow.Instance.RunEventLoop(true);

                if (FamiStudioWindow.Instance.IsWindowInFocus)
                {
                    if (!GtkWindowIsActive(dialog))
                    {
                        if (isCanberraAvailable && !alertSoundPlayed)
                        {
                            alertSoundPlayed = true;

                            if (isCanberraAvailable)
                            {
                                CanberraNative.PlaySound("bell");
                            }
                        }

                        // This doesn't work properly in all environments, but the dialog still remains above.
                        GtkWindowPresent(dialog);
                    }
                }
                else if (GtkWindowIsActive(dialog))
                {
                    alertSoundPlayed = false;
                }

                if ((isWayland && !isWaylandTransient) || !isGdkValid)
                    TrySimulateTransientBehaviour(dialog);
            }

            callbackHandle.Free();

            // File dialogs.
            if (dialogMode != DialogMode.Message && response == GTK_RESPONSE_ACCEPT)
            {
                if (GtkGetWidgetVisible(dialog))
                {
                    IntPtr list = GtkFileChooserGetFilenames(dialog);
                    if (list != IntPtr.Zero)
                    {
                        var pathsList = new List<string>();
                        IntPtr current = list;

                        while (current != IntPtr.Zero)
                        {
                            IntPtr filenamePtr = Marshal.ReadIntPtr(current);
                            string path = Marshal.PtrToStringUTF8(filenamePtr);
                            pathsList.Add(path);

                            current = Marshal.ReadIntPtr(current, IntPtr.Size);
                        }

                        paths = pathsList.ToArray();

                        //if (paths.Length > 0)
                            //dialogPath = Path.GetDirectoryName(paths[0]);

                        GSlistFreeFull(list, GFree);
                    }
                }
            }

            // Ensure we discard the dialog.
            if (IsGtkWidget(dialog))
            {
                GtkWidgetDestroy(dialog);

                while (GtkEventsPending())
                    GtkMainIteration();
            }

            if (isWayland && isWaylandTransient)
                WaylandForeign.Unexport();

            if (icon != IntPtr.Zero)
            {
                GObjectUnref(icon);
                icon = IntPtr.Zero;
            }

            return (paths, response);
        }

        private (string[] paths, int exitCode) ShowDialog(string command, string arguments)
        {
            var psi = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);

            // Skip running the loop if the window failed to initialize.
            if (FamiStudioWindow.Instance != null)
            {
                while (!process.HasExited)
                    FamiStudioWindow.Instance.RunEventLoop(true);
            }

            // If we're using flatpak, we may have converted the sandbox "/app" path
            // to a system one. If so, convert the results so the sandbox can find them.
            var results = process.StandardOutput.ReadToEnd().Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries);
            ConditionalRestoreFlatpakPaths(results);
            
            return (results, process.ExitCode);
        }
    }

    public static unsafe class WaylandForeign
    {
        const string Lib = "libwayland-client.so.0";

        [StructLayout(LayoutKind.Sequential)]
        struct WlMessage { public IntPtr name, signature, types; }

        [StructLayout(LayoutKind.Sequential)]
        struct WlInterface { public IntPtr name; public int version, methodCount; public IntPtr methods; public int eventCount; public IntPtr events; }

        [StructLayout(LayoutKind.Explicit)]
        struct WlArgument
        {
            [FieldOffset(0)] public int i;
            [FieldOffset(0)] public uint u;
            [FieldOffset(0)] public IntPtr o;
            [FieldOffset(0)] public IntPtr s;
        }

        [DllImport(Lib, EntryPoint = "wl_display_create_queue", CallingConvention = CallingConvention.Cdecl)] 
        static extern IntPtr WlDisplayCreateQueue(IntPtr display);

        [DllImport(Lib, EntryPoint = "wl_display_roundtrip_queue", CallingConvention = CallingConvention.Cdecl)] 
        static extern int WlDisplayRoundtripQueue(IntPtr display, IntPtr queue);

        [DllImport(Lib,EntryPoint = "wl_proxy_create_wrapper", CallingConvention = CallingConvention.Cdecl)] 
        static extern IntPtr WlProxyCreateWrapper(IntPtr proxy);

        [DllImport(Lib, EntryPoint = "wl_proxy_wrapper_destroy" ,CallingConvention = CallingConvention.Cdecl)] 
        static extern void WlProxyWrapperDestroy(IntPtr wrapper);

        [DllImport(Lib, EntryPoint = "wl_proxy_set_queue", CallingConvention = CallingConvention.Cdecl)] 
        static extern void WlProxySetQueue(IntPtr proxy, IntPtr queue);

        [DllImport(Lib, EntryPoint = "wl_proxy_add_listener", CallingConvention = CallingConvention.Cdecl)] 
        static extern int WlProxyAddListener(IntPtr proxy, IntPtr impl, IntPtr data);

        [DllImport(Lib, EntryPoint = "wl_proxy_destroy", CallingConvention = CallingConvention.Cdecl)] 
        static extern void WlProxyDestroy(IntPtr proxy);

        [DllImport(Lib, EntryPoint = "wl_proxy_marshal_array", CallingConvention = CallingConvention.Cdecl)] 
        static extern void WlProxyMarshalArray(IntPtr proxy, uint opcode, WlArgument* args);

        [DllImport(Lib, EntryPoint = "wl_proxy_marshal_array_constructor_versioned",CallingConvention = CallingConvention.Cdecl)] 
        static extern IntPtr WlProxyMarshalArrayConstructorVersioned(IntPtr proxy, uint opcode, WlArgument* args, IntPtr iface, uint version);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void GlobalFn(IntPtr data, IntPtr registry, uint name, IntPtr iface, uint version);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void GlobalRemoveFn(IntPtr data, IntPtr registry, uint name);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void HandleFn(IntPtr data, IntPtr exported, IntPtr handle);

        private static GlobalFn onGlobal;
        private static GlobalRemoveFn onGlobalRemove;
        private static HandleFn onHandle;

        private static IntPtr exporterIface, exportedIface, surfaceIface, registryIface;
        private static IntPtr queue, exporter, exported;
        private static string handle;
        private static bool initialized;

        static IntPtr Str(string s) => Marshal.StringToCoTaskMemUTF8(s);

        static IntPtr Messages(params (string name, string sig, IntPtr[] types)[] msgs)
        {
            var p = (WlMessage*)Marshal.AllocHGlobal(sizeof(WlMessage) * Math.Max(1, msgs.Length));
            for (int i = 0; i < msgs.Length; i++)
            {
                var t = Marshal.AllocHGlobal(IntPtr.Size * Math.Max(1, msgs[i].types.Length));
                Marshal.Copy(msgs[i].types, 0, t, msgs[i].types.Length);
                p[i] = new WlMessage { name = Str(msgs[i].name), signature = Str(msgs[i].sig), types = t };
            }
            return (IntPtr)p;
        }

        static IntPtr MakeInterface(string name, int version, IntPtr methods, int nMethods, IntPtr events, int nEvents)
        {
            var p = (WlInterface*)Marshal.AllocHGlobal(sizeof(WlInterface));
            *p = new WlInterface { name = Str(name), version = version, methodCount = nMethods, methods = methods, eventCount = nEvents, events = events };
            return (IntPtr)p;
        }

        static void Init()
        {
            if (initialized) return;
            var lib = NativeLibrary.Load(Lib);
            registryIface = NativeLibrary.GetExport(lib, "wl_registry_interface");
            surfaceIface  = NativeLibrary.GetExport(lib, "wl_surface_interface");

            // zxdg_exported_v2: request destroy(), event handle(string)
            exportedIface = MakeInterface("zxdg_exported_v2", 1, Messages(("destroy", "", new IntPtr[0])), 1,  Messages(("handle", "s", new[] { IntPtr.Zero })), 1);
            // zxdg_exporter_v2: request destroy(), export_toplevel(new_id exported, object surface)
            exporterIface = MakeInterface("zxdg_exporter_v2", 1, Messages(("destroy", "", new IntPtr[0]), ("export_toplevel", "no", new[] { exportedIface, surfaceIface })), 2, IntPtr.Zero, 0);

            initialized = true;
        }

        static IntPtr MakeListener(params Delegate[] fns)
        {
            var mem = Marshal.AllocHGlobal(IntPtr.Size * fns.Length);
            for (int i = 0; i < fns.Length; i++)
                Marshal.WriteIntPtr(mem, i * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(fns[i]));
            return mem;
        }

        public static string Export(IntPtr display, IntPtr surface)
        {
            if (display == IntPtr.Zero || surface == IntPtr.Zero)
                return null;
            if (exported != IntPtr.Zero)
                return handle;

            try
            {
                Init();

                if (queue == IntPtr.Zero)
                    queue = WlDisplayCreateQueue(display);

                if (exporter == IntPtr.Zero)
                {
                    var wrapper = WlProxyCreateWrapper(display);
                    WlProxySetQueue(wrapper, queue);

                    var args = stackalloc WlArgument[1];
                    args[0].o = IntPtr.Zero; // new_id placeholder.
                    // wl_display.get_registry is opcode 1.
                    var registry = WlProxyMarshalArrayConstructorVersioned(wrapper, 1, args, registryIface, 1);
                    WlProxyWrapperDestroy(wrapper);

                    onGlobal = (d, reg, name, iface, ver) =>
                    {
                        if (Marshal.PtrToStringUTF8(iface) != "zxdg_exporter_v2") return;
                        var a = stackalloc WlArgument[4];
                        a[0].u = name;
                        a[1].o = iface;
                        a[2].u = 1;
                        a[3].o = IntPtr.Zero;
                        // wl_registry.bind is opcode 0.
                        exporter = WlProxyMarshalArrayConstructorVersioned(reg, 0, a, exporterIface, 1);
                    };
                    onGlobalRemove = (d, reg, name) => { };
                    WlProxyAddListener(registry, MakeListener(onGlobal, onGlobalRemove), IntPtr.Zero);

                    WlDisplayRoundtripQueue(display, queue);
                    if (exporter == IntPtr.Zero)
                        return null; // Compositor has no xdg-foreign. Transient will not work.
                }
                
                var ea = stackalloc WlArgument[2];
                ea[0].o = IntPtr.Zero;
                ea[1].o = surface;
                // zxdg_exporter_v2.export_toplevel is opcode 1.
                exported = WlProxyMarshalArrayConstructorVersioned(exporter, 1, ea, exportedIface, 1);

                handle = null;
                onHandle = (d, e, h) => handle = Marshal.PtrToStringUTF8(h);

                WlProxyAddListener(exported, MakeListener(onHandle), IntPtr.Zero);
                WlDisplayRoundtripQueue(display, queue);

                return handle;
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
            {
                return null;
            }
        }

        public static void Unexport()
        {
            if (exported == IntPtr.Zero) return;
            var a = stackalloc WlArgument[1];
            WlProxyMarshalArray(exported, 0, a); // Destroy request.
            WlProxyDestroy(exported);
            exported = IntPtr.Zero;
            handle = null;
        }
    }

    public static class CanberraNative
    {
        private const string Library = "libcanberra.so.0";

        private static IntPtr _context;
        private static bool _initialized;
        private static readonly object Sync = new();

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ca_context_create(out IntPtr context);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ca_context_change_props_full(IntPtr context, IntPtr properties);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ca_context_play_full(IntPtr context, uint id, IntPtr properties, IntPtr callback, IntPtr userdata);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ca_context_destroy(IntPtr context);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ca_proplist_create(out IntPtr properties);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ca_proplist_sets(IntPtr properties, [MarshalAs(UnmanagedType.LPUTF8Str)] string key, [MarshalAs(UnmanagedType.LPUTF8Str)] string value);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ca_proplist_destroy(IntPtr properties);

        public static bool Initialize(string theme)
        {
            lock (Sync)
            {
                if (_initialized)
                    return _context != IntPtr.Zero;

                _initialized = true;
                IntPtr context = IntPtr.Zero;

                try
                {
                    if (ca_context_create(out context) != 0)
                        return false;

                    if (!string.IsNullOrWhiteSpace(theme))
                    {
                        IntPtr properties = IntPtr.Zero;

                        try
                        {
                            if (ca_proplist_create(out properties) != 0)
                                return false;

                            if (ca_proplist_sets(properties,"canberra.xdg-theme.name", theme) != 0)
                                return false;

                            if (ca_context_change_props_full(context, properties) != 0)
                                return false;
                        }
                        finally
                        {
                            if (properties != IntPtr.Zero)
                                ca_proplist_destroy(properties);
                        }
                    }

                    _context = context;
                    context = IntPtr.Zero;
                    return true;
                }
                catch (DllNotFoundException)
                {
                    return false;
                }
                catch (EntryPointNotFoundException)
                {
                    return false;
                }
                finally
                {
                    if (context != IntPtr.Zero)
                        ca_context_destroy(context);
                }
            }
        }

        public static bool PlaySound(string soundEventId = "dialog-question")
        {
            lock (Sync)
            {
                if (_context == IntPtr.Zero)
                    return false;

                IntPtr properties = IntPtr.Zero;

                try
                {
                    if (ca_proplist_create(out properties) != 0)
                        return false;

                    if (ca_proplist_sets(properties, "event.id", soundEventId) != 0)
                        return false;

                    return ca_context_play_full(_context, 0, properties, IntPtr.Zero, IntPtr.Zero) == 0;
                }
                catch (DllNotFoundException)
                {
                    return false;
                }
                catch (EntryPointNotFoundException)
                {
                    return false;
                }
                finally
                {
                    if (properties != IntPtr.Zero)
                        ca_proplist_destroy(properties);
                }
            }
        }

        public static void Shutdown()
        {
            lock (Sync)
            {
                if (_context != IntPtr.Zero)
                {
                    ca_context_destroy(_context);
                    _context = IntPtr.Zero;
                }

                _initialized = false;
            }
        }
    }
}
