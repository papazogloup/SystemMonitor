namespace SystemMonitor
{
    public partial class SettingsForm : Form
    {
        private MonitorSettings settings;
        private bool hasChanges = false;
        private readonly ColorDialog colorDialog = new();
        
        // Make fields nullable with ?
        private CheckBox? chkHorizontal;
        private DataGridView? dataGridBars;
        private CheckBox? chkAlerts;
        private NumericUpDown? numSnooze;
        private CheckBox? chkSound;
        private Button? btnOK;
        private Button? btnCancel;
        
        public SettingsForm(MonitorSettings currentSettings)
        {
            settings = currentSettings;
            InitializeComponent();
            LoadSettings();
        }

        private void InitializeComponent()
        {
            // Form settings
            this.Text = "System Monitor Settings";
            this.ClientSize = new Size(600, 400);
            this.MinimumSize = new Size(500, 350);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.StartPosition = FormStartPosition.CenterScreen;
            
            // Layout settings
            chkHorizontal = new CheckBox 
            { 
                Text = "Horizontal Layout", 
                Checked = settings.IsHorizontalLayout,
                Location = new Point(12, 12),
                AutoSize = true
            };
            
            // Bars grid
            dataGridBars = new DataGridView
            {
                Location = new Point(12, 40),
                Size = new Size(560, 200),  // Reduced width from 760 to 560
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                BackgroundColor = SystemColors.Window,
                AllowDrop = true,
                GridColor = SystemColors.Control,  // Make grid lines less visible
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    SelectionBackColor = SystemColors.Control,  // Light gray instead of blue
                    SelectionForeColor = SystemColors.ControlText  // Regular text color
                }
            };

            // Create custom column for drag handle
            var dragColumn = new DataGridViewImageColumn
            {
                Name = "DragHandle",
                HeaderText = "",
                Image = CreateDragHandleIcon(),
                Width = 30,
                FillWeight = 10
            };

            // Add event handlers for drag & drop
            dataGridBars.MouseDown += DataGridBars_MouseDown;
            dataGridBars.MouseMove += DataGridBars_MouseMove;
            dataGridBars.DragOver += DataGridBars_DragOver;
            dataGridBars.DragDrop += DataGridBars_DragDrop;

            dataGridBars.Columns.AddRange(new DataGridViewColumn[] {
                dragColumn,
                new DataGridViewTextBoxColumn { 
                    Name = "Type", 
                    HeaderText = "Type", 
                    ReadOnly = true,
                    FillWeight = 40,
                    DefaultCellStyle = new DataGridViewCellStyle { 
                        Alignment = DataGridViewContentAlignment.MiddleCenter 
                    }
                },
                new DataGridViewCheckBoxColumn { 
                    Name = "Visible", 
                    HeaderText = "Visible",
                    FillWeight = 15,
                    DefaultCellStyle = new DataGridViewCellStyle { 
                        Alignment = DataGridViewContentAlignment.MiddleCenter 
                    }
                },
                new DataGridViewTextBoxColumn {
                    Name = "Color",
                    HeaderText = "Color",
                    FillWeight = 10,  // Reduced from 15 to 10
                    ReadOnly = true,
                    DefaultCellStyle = new DataGridViewCellStyle { 
                        Alignment = DataGridViewContentAlignment.MiddleLeft,
                        Padding = new Padding(4, 2, 2, 2)
                    }
                },
                new DataGridViewTextBoxColumn {
                    Name = "Threshold", 
                    HeaderText = "Threshold",
                    FillWeight = 35,
                    DefaultCellStyle = new DataGridViewCellStyle { 
                        Alignment = DataGridViewContentAlignment.MiddleCenter 
                    }
                }
            });

            // Center align all column headers
            dataGridBars.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            // Alert settings panel - adjusted width
            var alertsPanel = new Panel
            {
                Location = new Point(12, 250),
                Size = new Size(560, 80),   // Reduced width from 760 to 560
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };

            chkAlerts = new CheckBox 
            { 
                Text = "Enable Alerts",
                Location = new Point(0, 0),
                AutoSize = true,
                Checked = settings.AlertSettings.IsEnabled
            };
            
            var lblSnooze = new Label
            {
                Text = "Snooze Minutes:",
                Location = new Point(0, 30),
                Size = new Size(90, 20),
                TextAlign = ContentAlignment.MiddleLeft
            };

            numSnooze = new NumericUpDown 
            { 
                Minimum = 1,
                Maximum = 60,
                Value = settings.AlertSettings.SnoozeMinutes,
                Location = new Point(95, 30),
                Size = new Size(60, 20)
            };
            
            chkSound = new CheckBox 
            { 
                Text = "Enable Sound",
                Location = new Point(200, 30),
                AutoSize = true,
                Checked = settings.AlertSettings.SoundEnabled
            };

            alertsPanel.Controls.AddRange(new Control[] { 
                chkAlerts, lblSnooze, numSnooze, chkSound 
            });
            
            // Buttons panel with Reset, Apply, OK, Cancel
            var buttonPanel = new Panel
            {
                Location = new Point(12, 340),
                Size = new Size(560, 40),    // Increased height from 35 to 40
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            var btnReset = new Button
            {
                Text = "Reset to Default",
                AutoSize = true,
                MinimumSize = new Size(120, 28),  // Increased height from 25 to 28
                MaximumSize = new Size(200, 28),
                Location = new Point(5, 6),        // Centered vertically (40-28)/2 = 6
                Anchor = AnchorStyles.Left,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoEllipsis = false
            };
            btnReset.Click += (s, e) => ResetToDefaults();

            btnOK = new Button 
            { 
                Text = "OK",
                DialogResult = DialogResult.OK,
                Size = new Size(75, 28),          // Increased height to 28
                Location = new Point(320, 6),      // Centered vertically
                Anchor = AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnOK.Click += (s, e) => {
                if (ApplySettings())
                {
                    this.Close();
                }
            };
            
            var btnApply = new Button
            {
                Text = "Apply",
                Size = new Size(75, 28),          // Increased height to 28
                Location = new Point(400, 6),      // Centered vertically
                Anchor = AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleCenter,
                Enabled = false // Initially disabled until changes are made
            };
            btnApply.Click += (s, e) => ApplySettings();
            
            btnCancel = new Button 
            { 
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Size = new Size(75, 28),          // Increased height to 28
                Location = new Point(480, 6),      // Centered vertically
                Anchor = AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnCancel.Click += (s, e) => this.Close();

            buttonPanel.Controls.AddRange(new Control[] { btnReset, btnOK, btnApply, btnCancel });
            
            // Add all controls
            this.Controls.AddRange(new Control[] {
                chkHorizontal,
                dataGridBars,
                alertsPanel,
                buttonPanel
            });

            // Add CellClick handler
            dataGridBars.CellClick += DataGridBars_CellClick;

            // Add handlers for change tracking
            dataGridBars.CellValueChanged += (s, e) => { hasChanges = true; btnApply.Enabled = true; };
            chkHorizontal.CheckedChanged += (s, e) => { hasChanges = true; btnApply.Enabled = true; };
            chkAlerts.CheckedChanged += (s, e) => { hasChanges = true; btnApply.Enabled = true; };
            numSnooze.ValueChanged += (s, e) => { hasChanges = true; btnApply.Enabled = true; };
            chkSound.CheckedChanged += (s, e) => { hasChanges = true; btnApply.Enabled = true; };
        }

        private Bitmap CreateDragHandleIcon()
        {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                using var pen = new Pen(Color.Gray, 2);
                for (int i = 0; i < 3; i++)
                {
                    g.DrawLine(pen, 4, 4 + (i * 4), 12, 4 + (i * 4));
                }
            }
            return bmp;
        }

        private int dragRowIndex = -1;

        private void DataGridBars_MouseDown(object? sender, MouseEventArgs e)
        {
            if (dataGridBars == null) return;

            var hitTest = dataGridBars.HitTest(e.X, e.Y);
            // Only allow drag from the drag handle column
            if (hitTest.ColumnIndex == 0 && hitTest.RowIndex >= 0)
            {
                dragRowIndex = hitTest.RowIndex;
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

            // Move the bar in the settings
            var bar = settings.Bars[dragRowIndex];
            settings.Bars.RemoveAt(dragRowIndex);
            settings.Bars.Insert(targetRowIndex, bar);

            // Reload the grid
            LoadSettings();

            // Select the moved row
            dataGridBars.Rows[targetRowIndex].Selected = true;
            dragRowIndex = -1;
        }

        private void LoadSettings()
        {
            if (dataGridBars == null) return;

            dataGridBars.Rows.Clear();
            foreach (var bar in settings.Bars)
            {
                var row = dataGridBars.Rows[dataGridBars.Rows.Add()];
                row.Cells["DragHandle"].Value = null;
                row.Cells["Type"].Value = bar.Type.ToString();
                row.Cells["Visible"].Value = bar.IsVisible;
                row.Cells["Threshold"].Value = bar.Threshold;
                
                // Custom drawing for color cell
                var colorCell = row.Cells["Color"];
                colorCell.Style.BackColor = Color.White;
                colorCell.Value = "⚙";  // Just the gear emoji without spaces
                colorCell.Tag = bar.Color;
            }

            // Add custom paint handler for color cells if not already added
            if (dataGridBars.Columns["Color"].DefaultCellStyle.Tag == null)
            {
                dataGridBars.CellPainting += (s, e) =>
                {
                    if (e.ColumnIndex == dataGridBars.Columns["Color"].Index && e.RowIndex >= 0)
                    {
                        e.PaintBackground(e.CellBounds, true);
                        
                        // Draw color square
                        var color = (Color)dataGridBars.Rows[e.RowIndex].Cells[e.ColumnIndex].Tag;
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

                        // Draw gear icon
                        using (var brush = new SolidBrush(Color.Gray))
                        {
                            var text = "⚙";
                            var font = e.CellStyle.Font;
                            var textRect = new Rectangle(
                                squareRect.Right + 4,
                                e.CellBounds.Y,
                                e.CellBounds.Width - squareRect.Width - 8,
                                e.CellBounds.Height
                            );
                            
                            e.Graphics.DrawString(text, font, brush, textRect, 
                                new StringFormat { 
                                    LineAlignment = StringAlignment.Center,
                                    Alignment = StringAlignment.Near
                                });
                        }

                        e.Handled = true;
                    }
                };
                dataGridBars.Columns["Color"].DefaultCellStyle.Tag = true;
            }
        }

        private void DataGridBars_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (dataGridBars == null || e.RowIndex < 0) return;

            if (e.ColumnIndex == dataGridBars.Columns["Color"].Index)
            {
                var cell = dataGridBars.Rows[e.RowIndex].Cells[e.ColumnIndex];
                colorDialog.Color = (Color)cell.Tag;
                
                if (colorDialog.ShowDialog() == DialogResult.OK)
                {
                    settings.Bars[e.RowIndex].Color = colorDialog.Color;
                    cell.Tag = colorDialog.Color;
                    dataGridBars.InvalidateCell(cell);
                }
            }
        }

        private bool ApplySettings()
        {
            try
            {
                if (Owner is SystemTrayApp app)
                {
                    var newSettings = GetSettings();
                    app.UpdateSettings(newSettings);
                    SettingsManager.SaveSettings(newSettings);
                    hasChanges = false;
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error applying settings: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        public MonitorSettings GetSettings()
        {
            if (chkHorizontal == null || dataGridBars == null || chkAlerts == null || 
                numSnooze == null || chkSound == null)
            {
                throw new InvalidOperationException("Form controls not properly initialized");
            }

            // Save all settings
            settings.IsHorizontalLayout = chkHorizontal.Checked;
            settings.AlertSettings.IsEnabled = chkAlerts.Checked;
            settings.AlertSettings.SnoozeMinutes = (int)numSnooze.Value;
            settings.AlertSettings.SoundEnabled = chkSound.Checked;
            
            // Update bars order and settings
            var newBars = new List<BarSettings>();
            for (int i = 0; i < dataGridBars.Rows.Count; i++)
            {
                var row = dataGridBars.Rows[i];
                var bar = settings.Bars.First(b => b.Type.ToString() == row.Cells["Type"].Value.ToString());
                bar.IsVisible = (bool)row.Cells["Visible"].Value;
                bar.Threshold = Convert.ToSingle(row.Cells["Threshold"].Value);
                bar.Color = (Color)row.Cells["Color"].Tag;
                newBars.Add(bar);
            }
            settings.Bars = newBars;
            
            return settings;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (hasChanges && DialogResult != DialogResult.OK && 
                MessageBox.Show("Do you want to save your changes?", "Save Changes",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                ApplySettings();
            }
        }

        private void ResetToDefaults()
        {
            settings = new MonitorSettings();
            
            // Update UI
            chkHorizontal.Checked = settings.IsHorizontalLayout;
            chkAlerts.Checked = settings.AlertSettings.IsEnabled;
            numSnooze.Value = settings.AlertSettings.SnoozeMinutes;
            chkSound.Checked = settings.AlertSettings.SoundEnabled;
            
            LoadSettings(); // Reload grid
        }
    }
}