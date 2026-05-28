using System;
using System.Reflection;
using System.Runtime.InteropServices;
using static KOTORModSync.Formats.NCS.NCSDecomp.DecompilerLogger;

namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    /// <summary>
    /// Windows Forms implementation of IRegistryDialogHandler.
    /// Provides a dialog with a "don't show again" checkbox using System.Windows.Forms.
    /// This implementation uses reflection to avoid hard dependencies on Windows Forms,
    /// making it work when Windows Forms is available without requiring it as a package reference.
    /// This implementation is only available on Windows platforms.
    /// </summary>
    public class WindowsFormsRegistryDialogHandler : IRegistryDialogHandler
    {
        private static readonly bool IsWindowsFormsAvailable = CheckWindowsFormsAvailability();

        /// <summary>
        /// Checks if System.Windows.Forms is available via reflection.
        /// </summary>
        private static bool CheckWindowsFormsAvailability()
        {
            try
            {
                Assembly.Load("System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
                Type formType = Type.GetType("System.Windows.Forms.Form, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
                return formType != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Shows a dialog with a message and a "don't show again" checkbox using Windows Forms via reflection.
        /// </summary>
        /// <param name="title">The dialog title</param>
        /// <param name="message">The message to display</param>
        /// <param name="dontShowAgain">Output parameter indicating whether the user checked "don't show again"</param>
        /// <returns>True if the dialog was shown successfully, false otherwise</returns>
        public bool ShowDialogWithDontShowAgain(string title, string message, out bool dontShowAgain)
        {
            dontShowAgain = false;

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Debug("[INFO] WindowsFormsRegistryDialogHandler: Not on Windows, cannot show dialog");
                return false;
            }

            if (!IsWindowsFormsAvailable)
            {
                Debug("[INFO] WindowsFormsRegistryDialogHandler: System.Windows.Forms not available");
                return false;
            }

            try
            {
                // Load Windows Forms types via reflection
                Assembly winFormsAssembly = Assembly.Load("System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
                Type formType = winFormsAssembly.GetType("System.Windows.Forms.Form");
                Type labelType = winFormsAssembly.GetType("System.Windows.Forms.Label");
                Type checkBoxType = winFormsAssembly.GetType("System.Windows.Forms.CheckBox");
                Type buttonType = winFormsAssembly.GetType("System.Windows.Forms.Button");
                Type panelType = winFormsAssembly.GetType("System.Windows.Forms.Panel");
                Type dialogResultType = winFormsAssembly.GetType("System.Windows.Forms.DialogResult");
                Type dockStyleType = winFormsAssembly.GetType("System.Windows.Forms.DockStyle");
                Type formBorderStyleType = winFormsAssembly.GetType("System.Windows.Forms.FormBorderStyle");
                Type formStartPositionType = winFormsAssembly.GetType("System.Windows.Forms.FormStartPosition");
                Type contentAlignmentType = Type.GetType("System.Drawing.ContentAlignment, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
                Type paddingType = Type.GetType("System.Windows.Forms.Padding, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
                Type sizeType = Type.GetType("System.Drawing.Size, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");

                if (formType == null || labelType == null || checkBoxType == null || buttonType == null || panelType == null)
                {
                    Debug("[INFO] WindowsFormsRegistryDialogHandler: Failed to load required Windows Forms types");
                    return false;
                }

                // Create form instance
                object dialog = Activator.CreateInstance(formType);
                SetProperty(dialog, "Text", title);
                SetProperty(dialog, "FormBorderStyle", GetEnumValue(formBorderStyleType, "FixedDialog"));
                SetProperty(dialog, "MaximizeBox", false);
                SetProperty(dialog, "MinimizeBox", false);
                SetProperty(dialog, "StartPosition", GetEnumValue(formStartPositionType, "CenterScreen"));
                SetProperty(dialog, "Width", 500);
                SetProperty(dialog, "Height", 250);
                SetProperty(dialog, "Padding", CreatePadding(paddingType, 15));

                // Create label
                object messageLabel = Activator.CreateInstance(labelType);
                SetProperty(messageLabel, "Text", message);
                SetProperty(messageLabel, "Dock", GetEnumValue(dockStyleType, "Fill"));
                SetProperty(messageLabel, "AutoSize", false);
                if (contentAlignmentType != null)
                {
                    SetProperty(messageLabel, "TextAlign", GetEnumValue(contentAlignmentType, "TopLeft"));
                }
                if (sizeType != null)
                {
                    SetProperty(messageLabel, "MaximumSize", CreateSize(sizeType, 470, 0));
                }

                // Create checkbox
                object dontShowCheckBox = Activator.CreateInstance(checkBoxType);
                SetProperty(dontShowCheckBox, "Text", "Don't show this message again");
                SetProperty(dontShowCheckBox, "Dock", GetEnumValue(dockStyleType, "Bottom"));
                SetProperty(dontShowCheckBox, "Height", 25);
                SetProperty(dontShowCheckBox, "Padding", CreatePadding(paddingType, 0, 5, 0, 0));

                // Create OK button
                object okButton = Activator.CreateInstance(buttonType);
                SetProperty(okButton, "Text", "OK");
                SetProperty(okButton, "DialogResult", GetEnumValue(dialogResultType, "OK"));
                SetProperty(okButton, "Dock", GetEnumValue(dockStyleType, "Bottom"));
                SetProperty(okButton, "Height", 30);
                SetProperty(okButton, "Margin", CreatePadding(paddingType, 0, 5, 0, 0));

                // Create panels
                object contentPanel = Activator.CreateInstance(panelType);
                SetProperty(contentPanel, "Dock", GetEnumValue(dockStyleType, "Fill"));
                SetProperty(contentPanel, "Padding", CreatePadding(paddingType, 0, 0, 0, 10));

                object buttonPanel = Activator.CreateInstance(panelType);
                SetProperty(buttonPanel, "Dock", GetEnumValue(dockStyleType, "Bottom"));
                SetProperty(buttonPanel, "Height", 30);

                // Add controls to panels
                // Controls is a property that returns a ControlCollection, and Add is a method on that collection
                object buttonPanelControls = GetProperty(buttonPanel, "Controls");
                object contentPanelControls = GetProperty(contentPanel, "Controls");
                object dialogControls = GetProperty(dialog, "Controls");
                
                InvokeMethod(buttonPanelControls, "Add", okButton);
                InvokeMethod(contentPanelControls, "Add", messageLabel);
                InvokeMethod(contentPanelControls, "Add", dontShowCheckBox);
                InvokeMethod(contentPanelControls, "Add", buttonPanel);
                InvokeMethod(dialogControls, "Add", contentPanel);

                // Set OK button as default
                SetProperty(dialog, "AcceptButton", okButton);
                SetProperty(dialog, "CancelButton", okButton);

                // Show dialog
                object result = InvokeMethod(dialog, "ShowDialog");

                // Get checkbox state
                dontShowAgain = (bool)GetProperty(dontShowCheckBox, "Checked");

                // Dispose form
                IDisposable disposable = dialog as IDisposable;
                if (disposable != null)
                {
                    disposable.Dispose();
                }

                object okResult = GetEnumValue(dialogResultType, "OK");
                return result != null && result.Equals(okResult);
            }
            catch (Exception e)
            {
                Debug("[INFO] WindowsFormsRegistryDialogHandler: Failed to show dialog: " + e.Message);
                return false;
            }
        }

        private static void SetProperty(object obj, string propertyName, object value)
        {
            Type type = obj.GetType();
            PropertyInfo prop = type.GetProperty(propertyName);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(obj, value);
            }
        }

        private static object GetProperty(object obj, string propertyName)
        {
            Type type = obj.GetType();
            PropertyInfo prop = type.GetProperty(propertyName);
            return prop?.GetValue(obj);
        }

        private static object InvokeMethod(object obj, string methodName, params object[] parameters)
        {
            Type type = obj.GetType();
            // Handle method overloading by finding the method with matching parameter types
            MethodInfo method = null;
            if (parameters != null && parameters.Length > 0)
            {
                Type[] paramTypes = new Type[parameters.Length];
                for (int i = 0; i < parameters.Length; i++)
                {
                    paramTypes[i] = parameters[i]?.GetType() ?? typeof(object);
                }
                method = type.GetMethod(methodName, paramTypes);
            }
            else
            {
                method = type.GetMethod(methodName, new Type[0]);
            }

            if (method != null)
            {
                return method.Invoke(obj, parameters);
            }
            return null;
        }

        private static object GetEnumValue(Type enumType, string valueName)
        {
            if (enumType == null) return null;
            return Enum.Parse(enumType, valueName);
        }

        private static object CreatePadding(Type paddingType, int all)
        {
            if (paddingType == null) return null;
            ConstructorInfo ctor = paddingType.GetConstructor(new Type[] { typeof(int) });
            return ctor?.Invoke(new object[] { all });
        }

        private static object CreatePadding(Type paddingType, int left, int top, int right, int bottom)
        {
            if (paddingType == null) return null;
            ConstructorInfo ctor = paddingType.GetConstructor(new Type[] { typeof(int), typeof(int), typeof(int), typeof(int) });
            return ctor?.Invoke(new object[] { left, top, right, bottom });
        }

        private static object CreateSize(Type sizeType, int width, int height)
        {
            if (sizeType == null) return null;
            ConstructorInfo ctor = sizeType.GetConstructor(new Type[] { typeof(int), typeof(int) });
            return ctor?.Invoke(new object[] { width, height });
        }
    }
}

