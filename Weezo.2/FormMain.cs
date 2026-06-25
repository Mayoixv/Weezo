using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace Weezo
{
    public partial class FormMain : Form
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        private const string ConnStr =
            "Server=(localdb)\\MSSQLLocalDB;" +
            "Database=WeezoDB;" +
            "Integrated Security=true;" +
            "TrustServerCertificate=true;";

        private const string RutaScript = @"cerebro.exe";
        private const string RutaPython = "";

        private System.Windows.Forms.Timer _trackerTimer = new();
        private System.Windows.Forms.Timer _refreshTimer = new();
        private System.Windows.Forms.Timer _weezTimer = new();
        private NotifyIcon _trayIcon = null!;
        private Label lblVentanaActual = null!;
        private PictureBox chartActividad = null!;
        private Point _dragOffset;

        public FormMain()
        {
            InitializeComponent();
            InicializarBaseDeDatos();

            // Primera vez: pedir nombre
            string primeraVez = ObtenerConfig("PrimeraVez", "1");
            if (primeraVez == "1" || string.IsNullOrWhiteSpace(ObtenerConfig("NombreUsuario", "")))
            {
                var bienvenida = new FormBienvenida();
                bienvenida.ShowDialog();
            }

            // Verificar API key — si no hay, pedirla
            if (string.IsNullOrWhiteSpace(ObtenerConfig("ApiKey", "")))
            {
                PedirApiKey();
            }

            ConfigurarFormulario();
            ConfigurarTray();
            ConfigurarTimers();
            ConfigurarWeezTimer();
            ActualizarIntervaloTimer();
            CargarGrafico();

            MostrarSaludoInicial();
        }

        private void MostrarSaludoInicial()
        {
            string nombre = ObtenerConfig("NombreUsuario", "amigo");
            // Se muestra después de 1.5 seg para que cargue la UI
            var t = new System.Windows.Forms.Timer { Interval = 1500 };
            t.Tick += (s, e) =>
            {
                t.Stop();
                _trayIcon.ShowBalloonTip(4000, $"¡Bienvenido, {nombre}! 👋",
                    "Weezo está activo y listo para acompañarte.", ToolTipIcon.Info);
            };
            t.Start();
        }

        private void ConfigurarFormulario()
        {
            Text = "Weezo";
            Size = new Size(520, 680);
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Color.FromArgb(30, 30, 30);
            StartPosition = FormStartPosition.CenterScreen;
            ForeColor = Color.White;

            var panelTop = new Panel
            {
                Size = new Size(520, 60),
                Location = new Point(0, 0),
                BackColor = Color.FromArgb(20, 20, 20)
            };
            panelTop.MouseDown += IniciarArrastre;
            panelTop.MouseMove += Arrastrar;

            var lblTitulo = new Label
            {
                Text = "⚡ Weezo Dashboard",
                ForeColor = Color.FromArgb(114, 137, 218),
                Font = new Font("Segoe UI", 15, FontStyle.Bold),
                Location = new Point(16, 14),
                AutoSize = true
            };
            lblTitulo.MouseDown += IniciarArrastre;
            lblTitulo.MouseMove += Arrastrar;

            var btnCerrar = new Button
            {
                Text = "✕",
                Location = new Point(472, 12),
                Size = new Size(36, 36),
                BackColor = Color.FromArgb(220, 53, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnCerrar.FlatAppearance.BorderSize = 0;
            btnCerrar.Click += (s, e) => Application.Exit();

            var btnMin = new Button
            {
                Text = "—",
                Location = new Point(430, 12),
                Size = new Size(36, 36),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11),
                Cursor = Cursors.Hand
            };
            btnMin.FlatAppearance.BorderSize = 0;
            btnMin.Click += (s, e) => WindowState = FormWindowState.Minimized;

            panelTop.Controls.AddRange(new Control[] { lblTitulo, btnCerrar, btnMin });

            var lblHeader = new Label
            {
                Text = "Ventana activa ahora:",
                ForeColor = Color.FromArgb(150, 150, 150),
                Font = new Font("Segoe UI", 9),
                Location = new Point(20, 72),
                AutoSize = true
            };

            lblVentanaActual = new Label
            {
                Text = "—",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Location = new Point(20, 90),
                Size = new Size(470, 22),
                AutoEllipsis = true
            };

            var btnRefrescar = new Button
            {
                Text = "↻  Refrescar",
                Location = new Point(20, 122),
                Size = new Size(140, 34),
                BackColor = Color.FromArgb(114, 137, 218),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnRefrescar.FlatAppearance.BorderSize = 0;
            btnRefrescar.Click += (s, e) => CargarGrafico();

            var btnWeezo = new Button
            {
                Text = "💜 Pedir opinión",
                Location = new Point(170, 122),
                Size = new Size(150, 34),
                BackColor = Color.FromArgb(88, 101, 242),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnWeezo.FlatAppearance.BorderSize = 0;
            btnWeezo.Click += async (s, e) =>
            {
                btnWeezo.Enabled = false;
                btnWeezo.Text = "Cargando...";
                string respuesta = await EjecutarCerebroAsync();
                btnWeezo.Enabled = true;
                btnWeezo.Text = "💜 Pedir opinión";
                if (!string.IsNullOrWhiteSpace(respuesta))
                {
                    MessageBox.Show(respuesta, "💜 Weezo dice~",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    MostrarNotificacion(respuesta);
                }
            };

            var btnConfig = new Button

            {
                Text = "⚙️",
                Location = new Point(330, 122),
                Size = new Size(40, 34),
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12),
                Cursor = Cursors.Hand
            };
            btnConfig.FlatAppearance.BorderSize = 0;
            btnConfig.Click += (s, e) =>

                
            {
                var formConfig = new FormConfig();
                if (formConfig.ShowDialog() == DialogResult.OK)
                    ActualizarIntervaloTimer();
            };

            var btnStats = new Button
            {
                Text = "📊",
                Location = new Point(380, 122),
                Size = new Size(40, 34),
                BackColor = Color.FromArgb(78, 205, 196),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12),
                Cursor = Cursors.Hand
            };
            btnStats.FlatAppearance.BorderSize = 0;
            btnStats.Click += (s, e) => new FormEstadisticas().ShowDialog();

            chartActividad = new PictureBox
            {
                Location = new Point(10, 165),
                Size = new Size(495, 390),
                BackColor = Color.FromArgb(30, 30, 30)
            };

            // Panel de meta diaria
            var panelMeta = new Panel
            {
                Location = new Point(10, 565),
                Size = new Size(495, 90),
                BackColor = Color.FromArgb(40, 40, 40)
            };

            var lblMeta = new Label
            {
                Name = "lblMeta",
                Text = "🎯 Meta diaria: cargando...",
                ForeColor = Color.FromArgb(114, 137, 218),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Location = new Point(10, 8),
                AutoSize = true
            };

            var progressMeta = new ProgressBar
            {
                Name = "progressMeta",
                Location = new Point(10, 28),
                Size = new Size(470, 20),
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Style = ProgressBarStyle.Continuous
            };

            var lblPorcentaje = new Label
            {
                Name = "lblPorcentaje",
                Text = "0%",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                Location = new Point(10, 54),
                AutoSize = true
            };

            panelMeta.Controls.AddRange(new Control[] { lblMeta, progressMeta, lblPorcentaje });

            Controls.AddRange(new Control[]
            {
               panelTop, lblHeader, lblVentanaActual,
                btnRefrescar, btnWeezo, btnConfig, btnStats,
                    chartActividad, panelMeta
            });
        }

        // ─────────────────────────────────────────────────────
        // SYSTEM TRAY
        // ─────────────────────────────────────────────────────
        private void ConfigurarTray()
        {
            _trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = "Weezo — Activo",
                Visible = true
            };

            var menu = new ContextMenuStrip();
            var itemMostrar = new ToolStripMenuItem("📋 Mostrar Dashboard");
            itemMostrar.Click += (s, e) =>
            {
                Show();
                WindowState = FormWindowState.Normal;
                BringToFront();
            };

            var itemSalir = new ToolStripMenuItem("✕ Salir");
            itemSalir.Click += (s, e) => Application.Exit();

            menu.Items.Add(itemMostrar);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(itemSalir);

            _trayIcon.ContextMenuStrip = menu;
            _trayIcon.DoubleClick += (s, e) =>
            {
                Show();
                WindowState = FormWindowState.Normal;
                BringToFront();
            };

            Resize += (s, e) =>
            {
                if (WindowState == FormWindowState.Minimized)
                {
                    Hide();
                    _trayIcon.ShowBalloonTip(2000, "Weezo sigue activo~",
                        "Estoy en la bandeja del sistema, my little boy 💜",
                        ToolTipIcon.Info);
                }
            };
        }

        // ─────────────────────────────────────────────────────
        // TIMERS
        // ─────────────────────────────────────────────────────
        private void ConfigurarTimers()
        {
            _trackerTimer.Interval = 1000;
            _trackerTimer.Tick += TrackerTimer_Tick;
            _trackerTimer.Start();

            _refreshTimer.Interval = 30000;
            _refreshTimer.Tick += (s, e) => CargarGrafico();
            _refreshTimer.Start();
        }

        private void ConfigurarWeezTimer()
        {
            _weezTimer.Interval = 300000;
            _weezTimer.Tick += WeezTimer_Tick;
            _weezTimer.Start();
        }

        private void TrackerTimer_Tick(object? sender, EventArgs e)
        {
            string titulo = ObtenerVentanaActiva();
            if (string.IsNullOrWhiteSpace(titulo)) return;
            if (titulo.Contains("Weezo", StringComparison.OrdinalIgnoreCase)) return;

            if (lblVentanaActual.InvokeRequired)
                lblVentanaActual.Invoke(() => lblVentanaActual.Text = titulo);
            else
                lblVentanaActual.Text = titulo;

            GuardarEnBD(titulo);

            string categoria = "ocio";
            if (titulo.Contains("Visual Studio", StringComparison.OrdinalIgnoreCase) ||
                titulo.Contains("VS Code", StringComparison.OrdinalIgnoreCase) ||
                titulo.Contains("SSMS", StringComparison.OrdinalIgnoreCase) ||
                titulo.Contains("sql", StringComparison.OrdinalIgnoreCase))
                categoria = "codigo";
            else if (titulo.Contains("Valorant", StringComparison.OrdinalIgnoreCase) ||
                     titulo.Contains("Steam", StringComparison.OrdinalIgnoreCase) ||
                     titulo.Contains("Overwatch", StringComparison.OrdinalIgnoreCase) ||
                     titulo.Contains("Epic Games", StringComparison.OrdinalIgnoreCase))
                categoria = "juego";

            ActualizarProgresoDiario(categoria);
            ActualizarBarraMeta();
        }

        private async void WeezTimer_Tick(object? sender, EventArgs e)
        {
            if (ObtenerConfig("ModoNoMolestar", "0") == "1") return;

            string respuesta = await EjecutarCerebroAsync();
            if (!string.IsNullOrWhiteSpace(respuesta))
                MostrarNotificacion(respuesta);
        }

        private string ObtenerVentanaActiva()
        {
            var sb = new StringBuilder(256);
            GetWindowText(GetForegroundWindow(), sb, 256);
            return sb.ToString();
        }

        private void GuardarEnBD(string titulo)
        {
            try
            {
                using var conn = new SqlConnection(ConnStr);
                conn.Open();
                using var cmd = new SqlCommand(
                    "INSERT INTO LogsActividad (VentanaTitulo) VALUES (@t)", conn);
                cmd.Parameters.AddWithValue("@t", titulo);
                cmd.ExecuteNonQuery();
            }
            catch { }
        }

        // ─────────────────────────────────────────────────────
        // CEREBRO.PY
        // ─────────────────────────────────────────────────────
        private async System.Threading.Tasks.Task<string> EjecutarCerebroAsync()
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "cerebro.exe"),
                    Arguments = "",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
                    StandardOutputEncoding = System.Text.Encoding.UTF8
                };

                using var proceso = new System.Diagnostics.Process { StartInfo = psi };
                proceso.Start();

                string salida = await proceso.StandardOutput.ReadToEndAsync();
                string errores = await proceso.StandardError.ReadToEndAsync();
                await proceso.WaitForExitAsync();

                if (!string.IsNullOrWhiteSpace(errores))
                {
                    MessageBox.Show($"Error de Python:\n\n{errores}", "Weezo — Debug",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return string.Empty;
                }

                if (string.IsNullOrWhiteSpace(salida))
                {
                    MessageBox.Show("Python corrió pero no devolvió nada.\nRuta:\n" + RutaScript,
                        "Weezo — Debug", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return string.Empty;
                }

                return salida.Trim();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo ejecutar Python:\n\n{ex.Message}",
                    "Weezo — Debug", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return string.Empty;
            }
        }

        private void MostrarNotificacion(string mensaje)
        {
            string textoCorto = mensaje.Length > 200 ? mensaje[..200] + "..." : mensaje;
            _trayIcon.ShowBalloonTip(8000, "💜 Weezo dice~", textoCorto, ToolTipIcon.None);
        }

        // ─────────────────────────────────────────────────────
        // GRÁFICO DONA
        // ─────────────────────────────────────────────────────
        private void CargarGrafico()
        {
            try
            {
                var datos = ObtenerDatosAgrupados();
                var bmp = new Bitmap(chartActividad.Width, chartActividad.Height);
                using var g = Graphics.FromImage(bmp);

                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                g.Clear(Color.FromArgb(30, 30, 30));

                if (datos.Count == 0)
                {
                    using var f = new Font("Segoe UI", 11);
                    g.DrawString("Sin datos aún — espera un momento...",
                        f, Brushes.Gray, new PointF(80, 170));
                    chartActividad.Image = bmp;
                    return;
                }

                Color[] colores =
                {
            Color.FromArgb(114, 137, 218),
            Color.FromArgb(255, 107, 107),
            Color.FromArgb(78,  205, 196),
            Color.FromArgb(255, 195,   0),
            Color.FromArgb(149, 117, 205)
        };

                int total = 0;
                var keys = new List<string>(datos.Keys);
                var values = new List<int>(datos.Values);
                foreach (var v in values) total += v;

                var rectDona = new Rectangle(135, 12, 220, 220);
                var rectHueco = new Rectangle(180, 57, 130, 130);
                float angulo = -90f;

                for (int i = 0; i < keys.Count; i++)
                {
                    float sweep = values[i] / (float)total * 360f;
                    using var br = new SolidBrush(colores[i % colores.Length]);
                    g.FillPie(br, rectDona, angulo, sweep);
                    angulo += sweep;
                }

                using var brHueco = new SolidBrush(Color.FromArgb(30, 30, 30));
                g.FillEllipse(brHueco, rectHueco);

                // Texto central: la categoría dominante
                string topCat = keys[0];
                float topPct = values[0] / (float)total * 100f;
                using var fTop = new Font("Segoe UI", 12, FontStyle.Bold);
                using var fPct = new Font("Segoe UI", 9);
                using var brW = new SolidBrush(Color.White);
                using var brGry = new SolidBrush(Color.FromArgb(160, 160, 160));
                var sf = new StringFormat { Alignment = StringAlignment.Center };

                g.DrawString($"{topPct:F0}%", fTop, brW,
                    new RectangleF(rectHueco.X, rectHueco.Y + 45, rectHueco.Width, 25), sf);
                g.DrawString(topCat, fPct, brGry,
                    new RectangleF(rectHueco.X, rectHueco.Y + 70, rectHueco.Width, 20), sf);

                // Leyenda
                using var fLeg = new Font("Segoe UI", 10);
                int ly = 250;
                for (int i = 0; i < keys.Count; i++)
                {
                    using var brColor = new SolidBrush(colores[i % colores.Length]);
                    g.FillRectangle(brColor, 30, ly, 18, 18);
                    float pct = values[i] / (float)total * 100f;
                    int min = values[i] / 60;
                    string tiempo = min >= 1 ? $"{min}m {values[i] % 60}s" : $"{values[i]}s";
                    g.DrawString($"{keys[i]}   {pct:F1}%   ({tiempo})",
                        fLeg, brW, 58, ly);
                    ly += 30;
                }

                chartActividad.Image = bmp;
            }
            catch { }
        }

        private Dictionary<string, int> ObtenerDatosAgrupados()
        {
            var resultado = new Dictionary<string, int>();
            using var conn = new SqlConnection(ConnStr);
            conn.Open();

            const string sql = @"
        SELECT
            CASE
                WHEN VentanaTitulo LIKE '%Visual Studio%'
                  OR VentanaTitulo LIKE '%VS Code%'
                  OR VentanaTitulo LIKE '%SSMS%'
                  OR VentanaTitulo LIKE '%sql%'        THEN 'Programando'
                WHEN VentanaTitulo LIKE '%Valorant%'
                  OR VentanaTitulo LIKE '%Overwatch%'
                  OR VentanaTitulo LIKE '%Steam%'
                  OR VentanaTitulo LIKE '%Epic Games%'
                  OR VentanaTitulo LIKE '%Wuthering%'
                  OR VentanaTitulo LIKE '%Street Fighter%' THEN 'Jugando'
                WHEN VentanaTitulo LIKE '%Chrome%'
                  OR VentanaTitulo LIKE '%Firefox%'
                  OR VentanaTitulo LIKE '%Brave%'
                  OR VentanaTitulo LIKE '%Edge%'       THEN 'Navegando'
                WHEN VentanaTitulo LIKE '%YouTube%'
                  OR VentanaTitulo LIKE '%Instagram%'
                  OR VentanaTitulo LIKE '%Twitter%'
                  OR VentanaTitulo LIKE '%TikTok%'
                  OR VentanaTitulo LIKE '%Discord%'    THEN 'Redes/Ocio'
                ELSE                                        'Otros'
            END AS Categoria,
            COUNT(*) AS Total
        FROM LogsActividad
        WHERE FechaHora >= DATEADD(HOUR, -1, SYSUTCDATETIME())
        GROUP BY
            CASE
                WHEN VentanaTitulo LIKE '%Visual Studio%'
                  OR VentanaTitulo LIKE '%VS Code%'
                  OR VentanaTitulo LIKE '%SSMS%'
                  OR VentanaTitulo LIKE '%sql%'        THEN 'Programando'
                WHEN VentanaTitulo LIKE '%Valorant%'
                  OR VentanaTitulo LIKE '%Overwatch%'
                  OR VentanaTitulo LIKE '%Steam%'
                  OR VentanaTitulo LIKE '%Epic Games%'
                  OR VentanaTitulo LIKE '%Wuthering%'
                  OR VentanaTitulo LIKE '%Street Fighter%' THEN 'Jugando'
                WHEN VentanaTitulo LIKE '%Chrome%'
                  OR VentanaTitulo LIKE '%Firefox%'
                  OR VentanaTitulo LIKE '%Brave%'
                  OR VentanaTitulo LIKE '%Edge%'       THEN 'Navegando'
                WHEN VentanaTitulo LIKE '%YouTube%'
                  OR VentanaTitulo LIKE '%Instagram%'
                  OR VentanaTitulo LIKE '%Twitter%'
                  OR VentanaTitulo LIKE '%TikTok%'
                  OR VentanaTitulo LIKE '%Discord%'    THEN 'Redes/Ocio'
                ELSE                                        'Otros'
            END
        ORDER BY Total DESC";

            using var cmd = new SqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                resultado[reader.GetString(0)] = reader.GetInt32(1);

            return resultado;
        }

        // ─────────────────────────────────────────────────────
        // METAS Y CONFIGURACIÓN
        // ─────────────────────────────────────────────────────
        private string ObtenerConfig(string clave, string defecto = "")
        {
            try
            {
                using var conn = new SqlConnection(ConnStr);
                conn.Open();
                using var cmd = new SqlCommand(
                    "SELECT Valor FROM Configuracion WHERE Clave = @k", conn);
                cmd.Parameters.AddWithValue("@k", clave);
                var result = cmd.ExecuteScalar();
                return result?.ToString() ?? defecto;
            }
            catch { return defecto; }
        }

        private void ActualizarIntervaloTimer()
        {
            int minutos = int.Parse(ObtenerConfig("IntervaloNotificacion", "5"));
            _weezTimer.Interval = minutos * 60 * 1000;
        }

        private void ActualizarProgresoDiario(string categoria)
        {
            try
            {
                using var conn = new SqlConnection(ConnStr);
                conn.Open();

                using var cmdCheck = new SqlCommand(@"
                    IF NOT EXISTS (SELECT 1 FROM ProgresosDiarios WHERE Fecha = CAST(GETDATE() AS DATE))
                        INSERT INTO ProgresosDiarios (Fecha) VALUES (CAST(GETDATE() AS DATE))", conn);
                cmdCheck.ExecuteNonQuery();

                string columna = categoria switch
                {
                    "codigo" => "MinutosCodigo",
                    "juego" => "MinutosJuego",
                    _ => "MinutosOcio"
                };

                using var cmdUpdate = new SqlCommand(
                    $"UPDATE ProgresosDiarios SET {columna} = {columna} + 1 " +
                    "WHERE Fecha = CAST(GETDATE() AS DATE)", conn);
                cmdUpdate.ExecuteNonQuery();
            }
            catch { }
        }

        private void ActualizarBarraMeta()
        {
            try
            {
                int meta = int.Parse(ObtenerConfig("MetaMinutosCodigo", "120"));

                using var conn = new SqlConnection(ConnStr);
                conn.Open();
                using var cmd = new SqlCommand(@"
                    SELECT ISNULL(MinutosCodigo, 0)
                    FROM   ProgresosDiarios
                    WHERE  Fecha = CAST(GETDATE() AS DATE)", conn);

                int segundos = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
                int minutos = segundos / 60;
                int porcentaje = Math.Min(100, (int)(minutos / (float)meta * 100));

                if (InvokeRequired)
                    Invoke(() => RefrescarBarraMeta(minutos, meta, porcentaje));
                else
                    RefrescarBarraMeta(minutos, meta, porcentaje);
            }
            catch { }
        }

        private void RefrescarBarraMeta(int minutos, int meta, int porcentaje)
        {
            string nombre = ObtenerConfig("NombreUsuario", "Starlyn");

            if (Controls.Find("progressMeta", true).FirstOrDefault() is ProgressBar pb)
                pb.Value = porcentaje;

            if (Controls.Find("lblMeta", true).FirstOrDefault() is Label lm)
                lm.Text = $" Meta de {nombre}: {minutos}/{meta} min de código hoy";

            if (Controls.Find("lblPorcentaje", true).FirstOrDefault() is Label lp)
            {
                lp.Text = porcentaje >= 100 ? "✅ ¡Meta cumplida! Good boy~ 💜" : $"{porcentaje}% completado";
                lp.ForeColor = porcentaje >= 100 ? Color.FromArgb(78, 205, 196) : Color.White;
            }
        }

        // ─────────────────────────────────────────────────────
        // ARRASTRE
        // ─────────────────────────────────────────────────────
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

        // ─────────────────────────────────────────────────────
        // CIERRE
        // ─────────────────────────────────────────────────────
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            base.OnFormClosing(e);
        }


        private void InicializarBaseDeDatos()
        {
            try
            {
                // Primero conectar a master para crear la BD si no existe
                string connMaster =
                    "Server=(localdb)\\MSSQLLocalDB;" +
                    "Database=master;" +
                    "Integrated Security=true;" +
                    "TrustServerCertificate=true;";

                using var connM = new SqlConnection(connMaster);
                connM.Open();

                using var cmdCreate = new SqlCommand(@"
            IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'WeezoDB')
                CREATE DATABASE WeezoDB;", connM);
                cmdCreate.ExecuteNonQuery();
                connM.Close();

                // Ahora conectar a WeezoDB y crear tablas
                using var conn = new SqlConnection(ConnStr);
                conn.Open();

                using var cmdTablas = new SqlCommand(@"
            -- LogsActividad
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LogsActividad')
            BEGIN
                CREATE TABLE LogsActividad (
                    Id            INT IDENTITY(1,1) PRIMARY KEY,
                    VentanaTitulo NVARCHAR(500) NOT NULL,
                    FechaHora     DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
                );
                CREATE NONCLUSTERED INDEX IX_LogsActividad_FechaHora
                    ON LogsActividad (FechaHora DESC);
            END

            -- Configuracion
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Configuracion')
            BEGIN
                CREATE TABLE Configuracion (
                    Clave  NVARCHAR(50)  PRIMARY KEY,
                    Valor  NVARCHAR(200) NOT NULL
                );
                INSERT INTO Configuracion (Clave, Valor) VALUES
                ('NombreUsuario',        'Usuario'),
                ('MetaMinutosCodigo',    '120'),
                ('IntervaloNotificacion','5'),
                ('HoraInicioActivo',     '08:00'),
                ('HoraFinActivo',        '23:00'),
                ('AppsBloqueadas',       '');
            END

            -- ProgresosDiarios
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ProgresosDiarios')
            BEGIN
                CREATE TABLE ProgresosDiarios (
                    Id            INT IDENTITY(1,1) PRIMARY KEY,
                    Fecha         DATE NOT NULL DEFAULT CAST(GETDATE() AS DATE),
                    MinutosCodigo INT NOT NULL DEFAULT 0,
                    MinutosJuego  INT NOT NULL DEFAULT 0,
                    MinutosOcio   INT NOT NULL DEFAULT 0,
                    PuntosGanados INT NOT NULL DEFAULT 0,
                    RachaActual   INT NOT NULL DEFAULT 0
                );
            END", conn);
                cmdTablas.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error iniciando la base de datos:\n\n{ex.Message}\n\n" +
                    "Asegúrate de tener SQL Server LocalDB instalado.\n" +
                    "Descárgalo en: aka.ms/sqllocaldb",
                    "Weezo — Setup",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                Application.Exit();
            }
        }

        private void PedirApiKey()
        {
            using var form = new Form
            {
                Text = "Weezo — Configurar API Key",
                Size = new Size(480, 290),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterScreen,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var lblInfo = new Label
            {
                Text = "Weezo necesita una API key de Groq para funcionar.\n" +
                            "Es gratis. Consíguela en el siguiente enlace:",
                ForeColor = Color.FromArgb(200, 200, 200),
                Font = new Font("Segoe UI", 10),
                Location = new Point(20, 20),
                Size = new Size(430, 50)
            };

            var linkGroq = new LinkLabel
            {
                Text = "https://console.groq.com/keys",
                Location = new Point(20, 75),
                Size = new Size(430, 24),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                LinkColor = Color.FromArgb(114, 137, 218)
            };
            linkGroq.LinkClicked += (s, e) =>
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://console.groq.com/keys",
                    UseShellExecute = true
                });
            };

            var txtKey = new TextBox
            {
                Location = new Point(20, 115),
                Size = new Size(430, 30),
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 11),
                PlaceholderText = "Pega aquí tu API key (gsk_...)"
            };

            var btnGuardar = new Button
            {
                Text = "Guardar y continuar",
                Location = new Point(20, 170),
                Size = new Size(430, 44),
                BackColor = Color.FromArgb(114, 137, 218),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnGuardar.FlatAppearance.BorderSize = 0;
            btnGuardar.Click += (s, e) =>
            {
                string key = txtKey.Text.Trim();
                if (string.IsNullOrWhiteSpace(key))
                {
                    MessageBox.Show("Por favor pega tu API key.", "Weezo",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                GuardarConfig("ApiKey", key);
                form.DialogResult = DialogResult.OK;
                form.Close();
            };

            form.Controls.AddRange(new Control[] { lblInfo, linkGroq, txtKey, btnGuardar });
            form.ShowDialog();
        }

        private void GuardarConfig(string clave, string valor)
        {
            try
            {
                using var conn = new SqlConnection(ConnStr);
                conn.Open();
                using var cmd = new SqlCommand(@"
            IF EXISTS (SELECT 1 FROM Configuracion WHERE Clave = @k)
                UPDATE Configuracion SET Valor = @v WHERE Clave = @k
            ELSE
                INSERT INTO Configuracion (Clave, Valor) VALUES (@k, @v)", conn);
                cmd.Parameters.AddWithValue("@k", clave);
                cmd.Parameters.AddWithValue("@v", valor);
                cmd.ExecuteNonQuery();
            }
            catch { }
        }
    }
}