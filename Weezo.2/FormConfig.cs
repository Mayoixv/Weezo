using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;

namespace Weezo
{
    public class FormConfig : Form
    {
        private const string ConnStr =
            "Server=(localdb)\\MSSQLLocalDB;" +
            "Database=WeezoDB;" +
            "Integrated Security=true;" +
            "TrustServerCertificate=true;";

        private TextBox txtNombre = null!;
        private NumericUpDown numMeta = null!;
        private NumericUpDown numIntervalo = null!;
        private ComboBox cmbModo = null!;
        private ComboBox cmbIdioma = null!;
        private CheckBox chkNoMolestar = null!;
        private CheckBox chkArranque = null!;
        private Point _dragOffset;

        private readonly string[] _modos =
            { "normal", "mommy", "estricto", "militar", "coach", "sarcastico" };

        public FormConfig()
        {
            ConfigurarUI();
            CargarConfiguracion();
        }

        private void ConfigurarUI()
        {
            Text = "Weezo — Configuración";
            Size = new Size(460, 640);
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Color.FromArgb(30, 30, 30);
            StartPosition = FormStartPosition.CenterParent;
            ForeColor = Color.White;

            // Panel superior
            var panelTop = new Panel
            {
                Size = new Size(460, 55),
                Location = new Point(0, 0),
                BackColor = Color.FromArgb(20, 20, 20)
            };
            panelTop.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) _dragOffset = e.Location; };
            panelTop.MouseMove += (s, e) => { if (e.Button == MouseButtons.Left) { var p = PointToScreen(e.Location); Location = new Point(p.X - _dragOffset.X, p.Y - _dragOffset.Y); } };

            var lblTitulo = new Label
            {
                Text = "⚙️ Configuración",
                ForeColor = Color.FromArgb(114, 137, 218),
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                Location = new Point(16, 12),
                AutoSize = true
            };
            lblTitulo.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) _dragOffset = e.Location; };
            lblTitulo.MouseMove += (s, e) => { if (e.Button == MouseButtons.Left) { var p = PointToScreen(e.Location); Location = new Point(p.X - _dragOffset.X, p.Y - _dragOffset.Y); } };

            var btnCerrar = new Button
            {
                Text = "✕",
                Location = new Point(412, 10),
                Size = new Size(36, 36),
                BackColor = Color.FromArgb(220, 53, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnCerrar.FlatAppearance.BorderSize = 0;
            btnCerrar.Click += (s, e) => Close();
            panelTop.Controls.AddRange(new Control[] { lblTitulo, btnCerrar });
            Controls.Add(panelTop);

            int y = 70;

            // Nombre
            AgregarLabel("Tu nombre:", 30, y);
            txtNombre = AgregarTextBox(30, y + 22, 400);
            y += 68;

            // Modo de habla
            AgregarLabel("Estilo de Weezo:", 30, y);
            cmbModo = AgregarCombo(30, y + 22, 400, new object[]
            {
                "Normal (neutral y directo)",
                "Mommy (cariñoso y dulce)",
                "Estricto (firme y exigente)",
                "Militar (autoritario)",
                "Coach (motivacional)",
                "Sarcástico (burlón con humor)"
            });
            y += 68;

            // Idioma
            AgregarLabel("Idioma:", 30, y);
            cmbIdioma = AgregarCombo(30, y + 22, 400, new object[]
            {
                "Español",
                "English"
            });
            y += 68;

            // Meta diaria
            AgregarLabel("Meta diaria de programación (minutos):", 30, y);
            numMeta = new NumericUpDown
            {
                Location = new Point(30, y + 22),
                Size = new Size(190, 30),
                Minimum = 10,
                Maximum = 600,
                Value = 120,
                Increment = 10,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(numMeta);
            y += 68;

            // Intervalo notificaciones
            AgregarLabel("Cada cuántos minutos opina Weezo:", 30, y);
            numIntervalo = new NumericUpDown
            {
                Location = new Point(30, y + 22),
                Size = new Size(190, 30),
                Minimum = 10,   // mínimo 10 para no saturar la API
                Maximum = 120,
                Value = 15,
                Increment = 5,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
                BorderStyle = BorderStyle.FixedSingle
            };
            var lblHint = new Label
            {
                Text = "(mínimo 10 min recomendado)",
                ForeColor = Color.FromArgb(120, 120, 120),
                Font = new Font("Segoe UI", 8),
                Location = new Point(230, y + 28),
                AutoSize = true
            };
            Controls.Add(numIntervalo);
            Controls.Add(lblHint);
            y += 68;

            // Checkboxes
            chkNoMolestar = AgregarCheck("🔕  Modo No Molestar (sin notificaciones)", 30, y);
            y += 36;
            chkArranque = AgregarCheck("🚀  Iniciar Weezo cuando prenda la PC", 30, y);
            y += 50;

            // Botón guardar
            var btnGuardar = new Button
            {
                Text = "💾  Guardar cambios",
                Location = new Point(30, y),
                Size = new Size(400, 44),
                BackColor = Color.FromArgb(114, 137, 218),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnGuardar.FlatAppearance.BorderSize = 0;
            btnGuardar.Click += GuardarConfiguracion;
            Controls.Add(btnGuardar);
        }

        // ── Helpers de UI ─────────────────────────────────────
        private Label AgregarLabel(string texto, int x, int y)
        {
            var lbl = new Label
            {
                Text = texto,
                ForeColor = Color.FromArgb(150, 150, 150),
                Font = new Font("Segoe UI", 9),
                Location = new Point(x, y),
                AutoSize = true
            };
            Controls.Add(lbl);
            return lbl;
        }

        private TextBox AgregarTextBox(int x, int y, int width)
        {
            var txt = new TextBox
            {
                Location = new Point(x, y),
                Size = new Size(width, 30),
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 11)
            };
            Controls.Add(txt);
            return txt;
        }

        private ComboBox AgregarCombo(int x, int y, int width, object[] items)
        {
            var cmb = new ComboBox
            {
                Location = new Point(x, y),
                Size = new Size(width, 30),
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat
            };
            cmb.Items.AddRange(items);
            cmb.SelectedIndex = 0;
            Controls.Add(cmb);
            return cmb;
        }

        private CheckBox AgregarCheck(string texto, int x, int y)
        {
            var chk = new CheckBox
            {
                Text = texto,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
                Location = new Point(x, y),
                AutoSize = true,
                Cursor = Cursors.Hand
            };
            Controls.Add(chk);
            return chk;
        }

        // ── Cargar / Guardar ──────────────────────────────────
        private void CargarConfiguracion()
        {
            try
            {
                using var conn = new SqlConnection(ConnStr);
                conn.Open();
                using var cmd = new SqlCommand("SELECT Clave, Valor FROM Configuracion", conn);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string clave = reader.GetString(0);
                    string valor = reader.GetString(1);
                    switch (clave)
                    {
                        case "NombreUsuario": txtNombre.Text = valor; break;
                        case "MetaMinutosCodigo": numMeta.Value = Clamp(valor, 10, 600, 120); break;
                        case "IntervaloNotificacion": numIntervalo.Value = Clamp(valor, 10, 120, 15); break;
                        case "ModoHabla": cmbModo.SelectedIndex = IndexDeModo(valor); break;
                        case "Idioma": cmbIdioma.SelectedIndex = valor == "en" ? 1 : 0; break;
                        case "ModoNoMolestar": chkNoMolestar.Checked = valor == "1"; break;
                    }
                }
            }
            catch { }

            // El arranque se lee del registro de Windows, no de la BD
            chkArranque.Checked = ArranqueActivado();
        }

        private decimal Clamp(string valor, int min, int max, int def)
        {
            if (!int.TryParse(valor, out int v)) v = def;
            return Math.Max(min, Math.Min(max, v));
        }

        private int IndexDeModo(string modo)
        {
            int i = Array.IndexOf(_modos, modo);
            return i < 0 ? 0 : i;
        }

        private void GuardarConfiguracion(object? sender, EventArgs e)
        {
            try
            {
                using var conn = new SqlConnection(ConnStr);
                conn.Open();

                void Upsert(string clave, string valor)
                {
                    using var cmd = new SqlCommand(@"
                        IF EXISTS (SELECT 1 FROM Configuracion WHERE Clave = @k)
                            UPDATE Configuracion SET Valor = @v WHERE Clave = @k
                        ELSE
                            INSERT INTO Configuracion (Clave, Valor) VALUES (@k, @v)", conn);
                    cmd.Parameters.AddWithValue("@k", clave);
                    cmd.Parameters.AddWithValue("@v", valor);
                    cmd.ExecuteNonQuery();
                }

                Upsert("NombreUsuario", txtNombre.Text.Trim());
                Upsert("MetaMinutosCodigo", ((int)numMeta.Value).ToString());
                Upsert("IntervaloNotificacion", ((int)numIntervalo.Value).ToString());
                Upsert("ModoHabla", _modos[cmbModo.SelectedIndex]);
                Upsert("Idioma", cmbIdioma.SelectedIndex == 1 ? "en" : "es");
                Upsert("ModoNoMolestar", chkNoMolestar.Checked ? "1" : "0");

                // Arranque con Windows → registro
                ConfigurarArranque(chkArranque.Checked);

                MessageBox.Show("¡Configuración guardada!", "Weezo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error guardando: {ex.Message}", "Weezo",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── Arranque con Windows ──────────────────────────────
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        private bool ArranqueActivado()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
                return key?.GetValue("Weezo") != null;
            }
            catch { return false; }
        }

        private void ConfigurarArranque(bool activar)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
                if (key == null) return;

                if (activar)
                {
                    string ruta = Application.ExecutablePath;
                    key.SetValue("Weezo", $"\"{ruta}\"");
                }
                else
                {
                    if (key.GetValue("Weezo") != null)
                        key.DeleteValue("Weezo");
                }
            }
            catch { }
        }
    }
}