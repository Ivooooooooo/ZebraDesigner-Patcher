using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Forms;

namespace ZebraPatcher
{
    public partial class MainForm : Form
    {
        private const string ExpectedMD5 = "12f1723e202cd110514bda9c9a2fc5e7";
        private const string BackupFileName = "Utils.backup.dll";
        private ModuleDefMD module;
        private Panel instructionsPanel;

        public MainForm()
        {
            InitializeComponent();
            InitializeInstructionsPanel();

            checkBox1.CheckedChanged += checkBox1_CheckedChanged;
        }

        private void InitializeInstructionsPanel()
        {
            instructionsPanel = new Panel();
            instructionsPanel.Location = new Point(15, 180);
            instructionsPanel.Size = new Size(385, 200);
            instructionsPanel.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(instructionsPanel);
            instructionsPanel.Visible = false;
        }

        private void ShowInstructionsPanel(bool show, MethodDef method = null)
        {
            instructionsPanel.Visible = show;

            if (show)
            {
                if (method != null)
                {
                    DisplayMethodInstructions(method);
                }

                Size = new Size(Size.Width, Size.Height + 105);
            }
            else
            {
                instructionsPanel.Controls.Clear();
                Size = new Size(Size.Width, Size.Height - 105);
            }
        }

        private void ShowInstructions(MethodDef method)
        {
            if (method != null)
            {
                DisplayMethodInstructions(method);
            }
        }

        private void DisplayMethodInstructions(MethodDef method)
        {
            instructionsPanel.Controls.Clear();

            TextBox instructionsTextBox = new TextBox();
            instructionsTextBox.Multiline = true;
            instructionsTextBox.ReadOnly = true;
            instructionsTextBox.ScrollBars = ScrollBars.Vertical;
            instructionsTextBox.Dock = DockStyle.Fill;

            foreach (Instruction instr in method.Body.Instructions)
            {
                instructionsTextBox.AppendText($"{instr.OpCode} {instr.Operand ?? ""}" + Environment.NewLine);
            }

            instructionsPanel.Controls.Add(instructionsTextBox);
        }

        private void browseFolder_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog())
            {
                DialogResult result = folderBrowserDialog.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(folderBrowserDialog.SelectedPath))
                {
                    folderZebraTxtBox.Text = folderBrowserDialog.SelectedPath;
                }
            }
        }

        private void patchButton_Click(object sender, EventArgs e)
        {
            string assemblyPath = Path.Combine(folderZebraTxtBox.Text, "Utils.dll");
            string backupPath = Path.Combine(folderZebraTxtBox.Text, BackupFileName);

            if (!File.Exists(assemblyPath))
            {
                LogError("El archivo Utils.dll no existe.");
                return;
            }

            string actualMD5 = CalculateMD5(assemblyPath);

            if (actualMD5 != ExpectedMD5)
            {
                DialogResult result = ShowWarningMessage("El MD5 del archivo Utils.dll es diferente al esperado. ¿Desea continuar?");

                if (result == DialogResult.No)
                {
                    LogInfo("Operación abortada por el usuario.");
                    return;
                }
            }

            try
            {
                if (!File.Exists(backupPath))
                {
                    BackupFile(assemblyPath, backupPath);
                }

                PatchModule(backupPath, assemblyPath);
            }
            catch (IOException ex)
            {
                LogError($"Error al realizar la copia de respaldo: {ex.Message}");
            }
            catch (Exception ex)
            {
                LogError($"Error desconocido: {ex.Message}");
            }
            finally
            {
                module?.Dispose();
            }
        }

        private void BackupFile(string sourcePath, string backupPath)
        {
            File.Copy(sourcePath, backupPath, true);
            LogSuccess("Copia de respaldo creada con éxito.");
        }

        private void PatchModule(string backupPath, string assemblyPath)
        {
            try
            {
                module = ModuleDefMD.Load(backupPath);

                PropertyDef propertyToPatch = module.ResolveToken(0x17000339) as PropertyDef;

                if (propertyToPatch != null)
                {
                    MethodDef getterMethod = propertyToPatch.GetMethod;

                    if (getterMethod != null)
                    {
                        if (getterMethod.Body.Instructions.Count == 17)
                        {
                            ShowInstructions(getterMethod);

                            PatchMethod(getterMethod);
                            module.Write(assemblyPath);

                            LogSuccess("¡Parcheado con éxito!");
                            statusLabel.Text = "Parcheado";

                            ShowInstructions(getterMethod);
                        }
                        else
                        {
                            LogError("La cantidad de instrucciones no es la esperada (17).");
                        }
                    }
                    else
                    {
                        LogError("No se encontró el getter de la propiedad para parchear.");
                    }
                }
                else
                {
                    LogError("No se encontró la propiedad para parchear.");
                }
            }
            catch (Exception ex)
            {
                LogError($"Error al parchear el archivo: {ex.Message}");
                module?.Dispose();
            }
        }

        private string CalculateMD5(string filePath)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    byte[] hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        private static void PatchMethod(MethodDef method)
        {
            var instructions = method.Body.Instructions;
            Instruction lastCall = null;
            Instruction lastLdsflda = null;

            for (int i = 0; i < instructions.Count; i++)
            {
                if (instructions[i].OpCode == OpCodes.Ldc_I4_0)
                {
                    instructions[i].OpCode = OpCodes.Ldc_I4_1;
                }

                if (instructions[i].OpCode == OpCodes.Call)
                {
                    lastCall = instructions[i];
                }

                if (instructions[i].OpCode == OpCodes.Ldsflda)
                {
                    lastLdsflda = instructions[i];
                }
            }

            if (lastCall != null)
            {
                int index = instructions.IndexOf(lastCall);
                instructions[index] = Instruction.Create(OpCodes.Ldc_I4_1);
            }

            if (lastLdsflda != null)
            {
                int index = instructions.IndexOf(lastLdsflda);
                instructions[index] = Instruction.Create(OpCodes.Nop);
            }
        }

        private DialogResult ShowWarningMessage(string message)
        {
            return MessageBox.Show(message, "Advertencia", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        }

        private void LogInfo(string message)
        {
            consoleLogTextBox.SelectionColor = Color.Black;
            consoleLogTextBox.AppendText(message + Environment.NewLine);
        }

        private void LogError(string message)
        {
            consoleLogTextBox.SelectionColor = Color.Red;
            consoleLogTextBox.AppendText($"ERROR: {message}" + Environment.NewLine);
        }

        private void LogSuccess(string message)
        {
            consoleLogTextBox.SelectionColor = Color.Green;
            consoleLogTextBox.AppendText($"ÉXITO: {message}" + Environment.NewLine);
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            CheckBox checkBox = (CheckBox)sender;
            ShowInstructionsPanel(checkBox.Checked);
        }
    }
}