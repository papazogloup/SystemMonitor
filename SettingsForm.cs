// System Monitor v2.0.0 - Settings Form
// Professional settings interface with smart change detection

using System.Diagnostics;

namespace SystemMonitor
{
    public partial class SettingsForm : Form
    {
        private MonitorSettings settings;
        private MonitorSettings tempSettings; // Clone αρχικών ρυθμίσεων
        private bool hasChanges = false;
        private bool hasUnsavedChanges = false;
        private readonly ColorDialog colorDialog = new();
        private CheckBox? chkHorizontal;
        private DataGridView? dataGridBars;
        private CheckBox? chkAlerts;
        private NumericUpDown? numSnooze;
        private CheckBox? chkSound;
        private Button? btnOK;
        private Button? btnCancel;
        private Button? btnApply;
        private GroupBox? linesPanel;
        private CheckBox? chkGuideLines;
        private CheckBox? chkThresholdLines;
        private CheckBox? chkPeakLines;
        private int dragRowIndex = -1;

        public SettingsForm(MonitorSettings currentSettings)
        {
            settings = currentSettings;
            tempSettings = currentSettings.Clone(); // Clone αρχικών ρυθμίσεων
            InitializeComponent();
            LoadSettings();
            // Apply should be disabled initially since no changes have been made
            btnApply.Enabled = false;
            hasUnsavedChanges = false;
        }

        private void LoadSettings()
        {
            if (dataGridBars == null) return;

            // Update checkboxes from tempSettings
            chkHorizontal.Checked = tempSettings.IsHorizontalLayout;
            chkGuideLines.Checked = tempSettings.ShowGuideLines;
            chkThresholdLines.Checked = tempSettings.ShowThresholdLines;
            chkPeakLines.Checked = tempSettings.ShowPeakLines;
            chkAlerts.Checked = tempSettings.AlertSettings.IsEnabled;
            chkSound.Checked = tempSettings.AlertSettings.SoundEnabled;
            numSnooze.Value = tempSettings.AlertSettings.SnoozeMinutes;

            dataGridBars.Rows.Clear();
            foreach (var bar in tempSettings.Bars) // Χρήση tempSettings
            {
                var row = dataGridBars.Rows[dataGridBars.Rows.Add()];
                var dragHandle = row.Cells["DragHandle"];
                if (dragHandle != null)
                {
                    dragHandle.Value = CreateDragHandleIcon();
                }
                if (row.Cells["Type"] != null)
                {
                    row.Cells["Type"].Value = bar.Type.ToDisplayString();
                }
                var visibleCell = row.Cells["Visible"];
                if (visibleCell != null)
                {
                    visibleCell.Value = bar.IsVisible;
                }
                var colorCell = row.Cells["Color"];
                if (colorCell != null)
                {
                    colorCell.Value = "■";
                    colorCell.Style.ForeColor = bar.Color;
                    colorCell.Tag = bar.Color;
                }
                var thresholdCell = row.Cells["Threshold"];
                if (thresholdCell != null)
                {
                    thresholdCell.Value = bar.Threshold;
                }
            }

            var colorColumn = dataGridBars?.Columns["Color"];
            if (colorColumn?.DefaultCellStyle.Tag == null)
            {
                dataGridBars!.CellPainting += (s, e) =>
                {
                    if (e.ColumnIndex == colorColumn.Index && e.RowIndex >= 0 && e.Graphics != null)
                    {
                        e.PaintBackground(e.CellBounds, true);

                        if (dataGridBars.Rows[e.RowIndex]?.Cells[e.ColumnIndex]?.Tag is Color color)
                        {
                            var squareSize = 14;
                            var squareRect = new Rectangle(
                                e.CellBounds.X + 4,
                                e.CellBounds.Y + (e.CellBounds.Height - squareSize) / 2,
                                squareSize,
                                squareSize
                            );

                            using (var brush = new SolidBrush(color))
                            {
                                e.Graphics.FillRectangle(brush, squareRect);
                                e.Graphics.DrawRectangle(Pens.Gray, squareRect);
                            }

                            using (var brush = new SolidBrush(Color.Gray))
                            {
                                var text = "⚙";
                                var font = e.CellStyle.Font ?? dataGridBars.DefaultCellStyle.Font ?? Control.DefaultFont;
                                var textRect = new Rectangle(
                                    squareRect.Right + 4,
                                    e.CellBounds.Y,
                                    e.CellBounds.Width - squareRect.Width - 8,
                                    e.CellBounds.Height
                                );

                                e.Graphics.DrawString(text, font, brush, textRect,
                                    new StringFormat
                                    {
                                        LineAlignment = StringAlignment.Center,
                                        Alignment = StringAlignment.Near
                                    });
                            }
                        }
                        e.Handled = true;
                    }
                };
                colorColumn.DefaultCellStyle.Tag = true;
            }
        }

        private void InitializeComponent()
        {
            // Form settings
            this.Text = "System Monitor Settings";
            this.ClientSize = new Size(600, 425);
            this.MinimumSize = new Size(500, 425);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.StartPosition = FormStartPosition.CenterScreen;

            // Layout checkbox at top
            chkHorizontal = new CheckBox
            {
                Text = "Horizontal Layout",
                Checked = settings.IsHorizontalLayout,
                Location = new Point(12, 12),
                AutoSize = true
            };

            // Grid with full width
            dataGridBars = new DataGridView
            {
                Location = new Point(12, 35),
                Size = new Size(576, 181),  // Height increased exactly by 25px (one row)
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, // Αφαίρεση του Bottom
                AllowDrop = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, // Άλλαξε σε Fill
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                EnableHeadersVisualStyles = false,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = SystemColors.Control,
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                },
                DefaultCellStyle = new DataGridViewCellStyle // Πρόσθεσε default style
                {
                    SelectionBackColor = SystemColors.Control, // Αφαίρεσε το μπλε highlight
                    SelectionForeColor = SystemColors.ControlText
                },
                RowTemplate = { Height = 25 },
                BackgroundColor = SystemColors.Window,
                BorderStyle = BorderStyle.Fixed3D,
                CellBorderStyle = DataGridViewCellBorderStyle.Single,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false
            };

            // Add columns
            dataGridBars.Columns.AddRange(new DataGridViewColumn[] {
                new DataGridViewImageColumn
                {
                    Name = "DragHandle",
                    HeaderText = "",
                    FillWeight = 10
                },
                new DataGridViewTextBoxColumn
                {
                    Name = "Type",
                    HeaderText = "Type",
                    FillWeight = 55,
                    ReadOnly = true
                },
                new DataGridViewCheckBoxColumn
                {
                    Name = "Visible",
                    HeaderText = "Visible",
                    FillWeight = 11,
                },
                new DataGridViewButtonColumn
                {
                    Name = "Color",
                    HeaderText = "Color",
                    FillWeight = 10,
                    FlatStyle = FlatStyle.Flat
                },
                new DataGridViewTextBoxColumn
                {
                    Name = "Threshold",
                    HeaderText = "Threshold",
                    FillWeight = 14,
                }
            });

            // Remove existing drag & drop handlers and add these:
            dataGridBars.MouseDown += DataGridBars_MouseDown;
            dataGridBars.MouseMove += DataGridBars_MouseMove;
            dataGridBars.DragOver += DataGridBars_DragOver;
            dataGridBars.DragDrop += DataGridBars_DragDrop;

            // Alert panel
            var alertPanel = new GroupBox
            {
                Text = "Alert Settings",
                Location = new Point(12, 225),
                Size = new Size(576, 65),
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };

            chkAlerts = new CheckBox
            {
                Text = "Enable Alerts",
                Location = new Point(20, 30),
                AutoSize = true,
                Checked = settings.AlertSettings.IsEnabled
            };

            var lblSnooze = new Label
            {
                Text = "Snooze Minutes:",
                Location = new Point(200, 32),  // Moved left
                AutoSize = true
            };

            numSnooze = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 60,
                Value = settings.AlertSettings.SnoozeMinutes,
                Location = new Point(317, 30),  // Moved left
                Size = new Size(50, 20)         // Reduced width
            };

            chkSound = new CheckBox
            {
                Text = "Enable Sound",
                Location = new Point(420, 30),  // Moved left
                AutoSize = true,
                Checked = settings.AlertSettings.SoundEnabled
            };

            alertPanel.Controls.AddRange(new Control[] {
                chkAlerts, lblSnooze, numSnooze, chkSound
            });

            // Lines panel
            linesPanel = new GroupBox
            {
                Text = "Display Options",
                Location = new Point(12, 300),
                Size = new Size(576, 65),
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };

            chkGuideLines = new CheckBox
            {
                Text = "Show Guide Lines",
                Location = new Point(20, 25),
                AutoSize = true,
                Checked = settings.ShowGuideLines
            };

            chkThresholdLines = new CheckBox
            {
                Text = "Show Threshold Lines",
                Location = new Point(200, 25),
                AutoSize = true,
                Checked = settings.ShowThresholdLines
            };

            chkPeakLines = new CheckBox
            {
                Text = "Show Peak Lines",
                Location = new Point(420, 25),
                AutoSize = true,
                Checked = settings.ShowPeakLines
            };

            linesPanel.Controls.AddRange(new Control[] {
                chkGuideLines, chkThresholdLines, chkPeakLines
            });

            // Button panel
            var buttonPanel = new Panel
            {
                Location = new Point(12, 370),
                Size = new Size(576, 40),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            var btnReset = new Button
            {
                Text = "Reset to Default",
                AutoSize = true,
                MinimumSize = new Size(120, 28),
                MaximumSize = new Size(200, 28),
                Location = new Point(5, 6),
                Anchor = AnchorStyles.Left,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnReset.Click += (s, e) => ResetToDefaults();

            btnOK = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Size = new Size(75, 28),
                Location = new Point(320, 6),
                Anchor = AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnOK.Click += (s, e) => {
                if (ApplySettings())
                {
                    this.Close();
                }
            };

            btnApply = new Button
            {
                Text = "Apply",
                Size = new Size(75, 28),
                Location = new Point(400, 6),
                Anchor = AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleCenter,
                Enabled = false
            };
            btnApply.Click += (s, e) => ApplySettings();

            btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Size = new Size(75, 28),
                Location = new Point(480, 6),
                Anchor = AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnCancel.Click += (s, e) => this.Close();

            // Add controls to form
            this.Controls.AddRange(new Control[] {
                chkHorizontal,
                dataGridBars,
                alertPanel,
                linesPanel,
                buttonPanel
            });

            buttonPanel.Controls.AddRange(new Control[] {
                btnReset, btnOK, btnApply, btnCancel
            });

            // Attach change handlers at the end
            AttachChangeHandlers();
        }

        // Add this method to check if current values differ from original settings:
        private bool HasChanges()
        {
            // Compare tempSettings with the ORIGINAL settings (not current)
            // We need to track what the settings were when form opened
            
            // Check alert settings
            if (tempSettings.AlertSettings.IsEnabled != settings.AlertSettings.IsEnabled ||
                tempSettings.AlertSettings.SoundEnabled != settings.AlertSettings.SoundEnabled ||
                tempSettings.AlertSettings.SnoozeMinutes != settings.AlertSettings.SnoozeMinutes)
            {
                return true;
            }

            // Check bars settings
            for (int i = 0; i < settings.Bars.Count && i < tempSettings.Bars.Count; i++)
            {
                var originalBar = settings.Bars[i];
                var tempBar = tempSettings.Bars[i];
                
                if (tempBar.IsVisible != originalBar.IsVisible ||
                    Math.Abs(tempBar.Threshold - originalBar.Threshold) > 0.1f ||
                    tempBar.Color != originalBar.Color)
                {
                    return true;
                }
            }

            // Check layout settings
            if (tempSettings.IsHorizontalLayout != settings.IsHorizontalLayout ||
                tempSettings.ShowGuideLines != settings.ShowGuideLines ||
                tempSettings.ShowPeakLines != settings.ShowPeakLines ||
                tempSettings.ShowThresholdLines != settings.ShowThresholdLines)
            {
                return true;
            }

            return false;
        }

        // Add this method to update Apply button state:
        private void UpdateApplyButtonState()
        {
            hasUnsavedChanges = HasChanges();
            btnApply.Enabled = hasUnsavedChanges;
        }

        // Add change event handlers to all controls:
        private void AttachChangeHandlers()
        {
            // Alert settings - NO IMMEDIATE UPDATES, only tempSettings changes
            chkAlerts.CheckedChanged += (s, e) => {
                tempSettings.AlertSettings.IsEnabled = chkAlerts.Checked;
                UpdateApplyButtonState();
            };
            
            chkSound.CheckedChanged += (s, e) => {
                tempSettings.AlertSettings.SoundEnabled = chkSound.Checked;
                UpdateApplyButtonState();
            };
            
            numSnooze.ValueChanged += (s, e) => {
                tempSettings.AlertSettings.SnoozeMinutes = (int)numSnooze.Value;
                UpdateApplyButtonState();
            };
            
            // Layout settings
            chkHorizontal.CheckedChanged += (s, e) => {
                tempSettings.IsHorizontalLayout = chkHorizontal.Checked;
                UpdateApplyButtonState();
            };
            chkGuideLines.CheckedChanged += (s, e) => {
                tempSettings.ShowGuideLines = chkGuideLines.Checked;
                UpdateApplyButtonState();
            };
            chkPeakLines.CheckedChanged += (s, e) => {
                tempSettings.ShowPeakLines = chkPeakLines.Checked;
                UpdateApplyButtonState();
            };
            chkThresholdLines.CheckedChanged += (s, e) => {
                tempSettings.ShowThresholdLines = chkThresholdLines.Checked;
                UpdateApplyButtonState();
            };
            
            // DataGridView changes
            dataGridBars.CellValueChanged += (s, e) => {
                if (e.RowIndex >= 0 && e.RowIndex < tempSettings.Bars.Count)
                {
                    if (e.ColumnIndex == dataGridBars.Columns["Visible"].Index)
                    {
                        var cell = dataGridBars.Rows[e.RowIndex].Cells[e.ColumnIndex];
                        if (cell.Value != null)
                        {
                            tempSettings.Bars[e.RowIndex].IsVisible = Convert.ToBoolean(cell.Value);
                        }
                    }
                    else if (e.ColumnIndex == dataGridBars.Columns["Threshold"].Index)
                    {
                        var cell = dataGridBars.Rows[e.RowIndex].Cells[e.ColumnIndex];
                        if (float.TryParse(cell.Value?.ToString(), out float threshold))
                        {
                            tempSettings.Bars[e.RowIndex].Threshold = threshold;
                        }
                    }
                }
                UpdateApplyButtonState();
            };
            
            dataGridBars.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (dataGridBars.IsCurrentCellDirty)
                {
                    dataGridBars.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };

            // Keep the existing color and threshold handlers for immediate UI feedback
            dataGridBars.CellClick += (s, e) => {
                if (dataGridBars == null || e.RowIndex < 0) return;

                if (e.ColumnIndex == dataGridBars.Columns["Color"].Index)
                {
                    var cell = dataGridBars.Rows[e.RowIndex].Cells[e.ColumnIndex];
                    if (cell.Tag is Color currentColor)
                    {
                        colorDialog.Color = currentColor;
                        if (colorDialog.ShowDialog() == DialogResult.OK)
                        {
                            tempSettings.Bars[e.RowIndex].Color = colorDialog.Color;
                            cell.Tag = colorDialog.Color;
                            dataGridBars.InvalidateCell(cell);
                            UpdateApplyButtonState();
                        }
                    }
                }
            };

            dataGridBars.CellEndEdit += (s, e) => {
                if (e.ColumnIndex == dataGridBars.Columns["Threshold"].Index)
                {
                    var cell = dataGridBars.Rows[e.RowIndex].Cells[e.ColumnIndex];
                    if (float.TryParse(cell.Value?.ToString(), out float threshold))
                    {
                        tempSettings.Bars[e.RowIndex].Threshold = threshold;
                        UpdateApplyButtonState();
                    }
                }
            };
        }

        private bool ApplySettings()
        {
            try
            {
                settings.IsHorizontalLayout = tempSettings.IsHorizontalLayout;
                settings.ShowGuideLines = tempSettings.ShowGuideLines;
                settings.ShowThresholdLines = tempSettings.ShowThresholdLines;
                settings.ShowPeakLines = tempSettings.ShowPeakLines;
                settings.Bars = tempSettings.Bars.ToList(); // Copy the list including IsVisible changes
                settings.AlertSettings = tempSettings.AlertSettings;

                SettingsManager.SaveSettings(settings);
                
                // Update the main form with new settings
                if (Owner is SystemTrayApp mainForm)
                {
                    mainForm.UpdateSettings(settings);
                }

                // IMPORTANT: Update tempSettings to match the new applied settings
                // This ensures HasChanges() works correctly after Apply
                tempSettings = settings.Clone();
                
                hasChanges = false;
                hasUnsavedChanges = false;
                if (btnApply != null)
                {
                    btnApply.Enabled = false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving settings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (DialogResult == DialogResult.Cancel && hasChanges)
            {
                if (MessageBox.Show("Do you want to save your changes?", "Save Changes",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    ApplySettings();
                }
            }
        }

        private void ResetToDefaults()
        {
            // Create new settings with defaults
            tempSettings = new MonitorSettings();
            
            // Reload UI with new settings
            LoadSettings();
            
            // Mark as changed to enable Apply button
            hasChanges = true;
            if (btnApply != null)
            {
                btnApply.Enabled = true;
            }
        }

        private Bitmap CreateDragHandleIcon()
        {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                using var pen = new Pen(Color.Gray, 1);
                for (int i = 0; i < 3; i++)
                {
                    g.DrawLine(pen, 4, 6 + (i * 3), 12, 6 + (i * 3));
                }
            }
            return bmp;
        }

        private void DataGridBars_MouseDown(object? sender, MouseEventArgs e)
        {
            if (dataGridBars == null) return;

            var hitTest = dataGridBars.HitTest(e.X, e.Y);
            if (hitTest.ColumnIndex == 0 && hitTest.RowIndex >= 0)
            {
                dragRowIndex = hitTest.RowIndex;
                dataGridBars.Rows[dragRowIndex].Selected = true;
            }
            else
            {
                dragRowIndex = -1;
            }
        }

        private void DataGridBars_MouseMove(object? sender, MouseEventArgs e)
        {
            if (dataGridBars == null) return;

            // If the mouse is moved with button pressed and we have a valid row
            if ((e.Button & MouseButtons.Left) == MouseButtons.Left && dragRowIndex >= 0)
            {
                // Start the drag drop
                dataGridBars.DoDragDrop(dataGridBars.Rows[dragRowIndex], DragDropEffects.Move);
            }
        }

        private void DataGridBars_DragOver(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetDataPresent(typeof(DataGridViewRow)) == true)
            {
                e.Effect = DragDropEffects.Move;
            }
        }

        private void DataGridBars_DragDrop(object? sender, DragEventArgs e)
        {
            if (dataGridBars == null) return;

            Point clientPoint = dataGridBars.PointToClient(new Point(e.X, e.Y));
            int targetRowIndex = dataGridBars.HitTest(clientPoint.X, clientPoint.Y).RowIndex;

            if (targetRowIndex < 0 || dragRowIndex < 0 || targetRowIndex == dragRowIndex)
                return;

            // Get the row being dragged
            var rowData = tempSettings.Bars[dragRowIndex]; // Χρήση tempSettings
            
            // Remove from old position and insert at new position
            tempSettings.Bars.RemoveAt(dragRowIndex);
            tempSettings.Bars.Insert(targetRowIndex, rowData);

            // Refresh grid and mark changes
            LoadSettings();
            hasChanges = true;
            if (btnApply != null)
            {
                btnApply.Enabled = true;
            }

            // Select the moved row
            dataGridBars.Rows[targetRowIndex].Selected = true;
            dragRowIndex = -1;
        }
    }
}