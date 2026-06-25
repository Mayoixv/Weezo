using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace Weezo
{
    public class FormBienvenida : Form
    {
        private const string ConnStr =
            "Server=(localdb)\\MSSQLLocalDB;" +
            "Database=WeezoDB;" +
            "Integrated Security=true;" +
            "TrustServerCertificate=true;";

        private TextBox txtNombre = null!;
        private ComboBox cmbModo = null!;
        private Point _dragOffset;

        public string NombreUsuario { get; private set; } = "";

        public FormBienvenida()
        {
            ConfigurarUI();
        }

        private void ConfigurarUI()
        {
            Text = "Bienvenido a Weezo";
            Size = new Size(460, 420);
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Color.FromArgb(30, 30, 30);
            StartPosition = FormStartPosition.CenterScreen;
            ForeColor = Color.White;

            this.MouseDown += IniciarArrastre;
            this.MouseMove += Arrastrar;

            // Logo / título
            var lblLogo = new Label
            {
                Text = "⚡",
                ForeColor = Color.FromArgb(114, 137, 218),
                Font = new Font("Segoe UI", 40, FontStyle.Bold),
                Location = new Point(0, 30),
                Size = new Size(460, 70),
                TextAlign = ContentAlignment.MiddleCenter
            };

            var lblTitulo = new Label
            {
                Text = "Bienvenido a Weezo",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                Location = new Point(0, 100),
                Size = new Size(460, 35),
                TextAlign = ContentAlignment.MiddleCenter
            };

            var lblSub = new Label
            {
                Text = "Tu asistente de productividad con IA",
                ForeColor = Color.FromArgb(150, 150, 150),
                Font = new Font("Segoe UI", 10),
                Location = new Point(0, 135),
                Size = new Size(460, 25),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Campo nombre
            var lblNombre = new Label
            {
                Text = "¿Cómo te llamas?",
                ForeColor = Color.FromArgb(180, 180, 180),
                Font = new Font("Segoe UI", 10),
                Location = new Point(50, 185),
                AutoSize = true
            };

            txtNombre = new TextBox
            {
                Location = new Point(50, 210),
                Size = new Size(360, 32),
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 12)
            };

            // Selector de modo
            var lblModo = new Label
            {
                Text = "Estilo de Weezo:",
                ForeColor = Color.FromArgb(180, 180, 180),
                Font = new Font("Segoe UI", 10),
                Location = new Point(50, 255),
                AutoSize = true
            };

            cmbModo = new ComboBox
            {
                Location = new Point(50, 280),
                Size = new Size(360, 32),
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat
            };
            cmbModo.Items.AddRange(new object[]
            {
                "Normal (neutral y directo)",
                "Mommy (cariñoso y dulce)",
                "Estricto (firme y exigente)",
                "Militar (autoritario)",
                "Coach (motivacional)",
                "Sarcástico (burlón con humor)"
            });
            cmbModo.SelectedIndex = 0;

            // Botón empezar
            var btnEmpezar = new Button
            {
                Text = "Comenzar  →",
                Location = new Point(50, 335),
                Size = new Size(360, 44),
                BackColor = Color.FromArgb(114, 137, 218),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnEmpezar.FlatAppearance.BorderSize = 0;
            btnEmpezar.Click += BtnEmpezar_Click;

            Controls.AddRange(new Control[]
            {
                lblLogo, lblTitulo, lblSub,
                lblNombre, txtNombre,
                lblModo, cmbModo, btnEmpezar
            });
        }

        private void BtnEmpezar_Click(object? sender, EventArgs e)
        {
            string nombre = txtNombre.Text.Trim();
            if (string.IsNullOrWhiteSpace(nombre))
            {
                MessageBox.Show("Por favor escribe tu nombre.", "Weezo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string[] modos = { "normal", "mommy", "estricto", "militar", "coach", "sarcastico" };
            string modoSel = modos[cmbModo.SelectedIndex];

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

                Upsert("NombreUsuario", nombre);
                Upsert("ModoHabla", modoSel);
                Upsert("PrimeraVez", "0");

                NombreUsuario = nombre;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error guardando: {ex.Message}", "Weezo",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void IniciarArrastre(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) _dragOffset = e.Location;
        }

        private void Arrastrar(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                var pos = PointToScreen(e.Location);
                Location = new Point(pos.X - _dragOffset.X, pos.Y - _dragOffset.Y);
            }
        }
    }
}