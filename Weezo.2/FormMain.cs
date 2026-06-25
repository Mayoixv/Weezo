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
        // ── Win32 API ────────────────────────────────────────
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(
            int nLeftRect, int nTopRect, int nRightRect, int nBottomRect,
            int nWidthEllipse, int nHeightEllipse);

        // ── Configuración ─────────────────────────────────────
        private const string ConnStr =
            "Server=(localdb)\\MSSQLLocalDB;" +
            "Database=WeezoDB;" +
            "Integrated Security=true;" +
            "TrustServerCertificate=true;";

        private static readonly string RutaExe =
            System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cerebro.exe");

        // ── Paleta de colores ─────────────────────────────────
        private static readonly Color CFondo = Color.FromArgb(24, 24, 27);   // fondo ventana
        private static readonly Color CTarjeta = Color.FromArgb(36, 36, 40);   // tarjetas
        private static readonly Color CTopBar = Color.FromArgb(30, 30, 34);   // barra superior
        private static readonly Color CBlurple = Color.FromArgb(114, 137, 218);
        private static readonly Color CBlurple2 = Color.FromArgb(88, 101, 242);
        private static readonly Color CTeal = Color.FromArgb(78, 205, 196);
        private static readonly Color CTexto = Color.FromArgb(235, 235, 235);
        private static readonly Color CTextoSec = Color.FromArgb(140, 140, 145);

        // ── Controles y campos ────────────────────────────────
        private System.Windows.Forms.Timer _trackerTimer = new();
        private System.Windows.Forms.Timer _refreshTimer = new();
        private System.Windows.Forms.Timer _weezTimer = new();
        private NotifyIcon _trayIcon = null!;
        private Label lblVentanaActual = null!;
        private PictureBox chartActividad = null!;
        private Point _dragOffset;

        // ── Constructor ───────────────────────────────────────
        public FormMain()
        {
            InitializeComponent();
            InicializarBaseDeDatos();

            string primeraVez = ObtenerConfig("PrimeraVez", "1");
            if (primeraVez == "1" || string.IsNullOrWhiteSpace(ObtenerConfig("NombreUsuario", "")))
            {
                var bienvenida = new FormBienvenida();
                bienvenida.ShowDialog();
            }

            if (string.IsNullOrWhiteSpace(ObtenerConfig("ApiKey", "")))
                PedirApiKey();

            ConfigurarFormulario();
            RedondearVentana();
            ConfigurarTray();
            ConfigurarTimers();
            ConfigurarWeezTimer();
            ActualizarIntervaloTimer();
            CargarGrafico();

            Resize += (s, e) => RedondearVentana();
            MostrarSaludoInicial();
        }

        private void RedondearVentana()
        {
            Region = System.Drawing.Region.FromHrgn(
                CreateRoundRectRgn(0, 0, Width + 1, Height + 1, 18, 18));
        }

        private void RedondearControl(Control ctrl, int radio)
        {
            ctrl.Region = System.Drawing.Region.FromHrgn(
                CreateRoundRectRgn(0, 0, ctrl.Width, ctrl.Height, radio, radio));
        }

        // ─────────────────────────────────────────────────────
        // UI
        // ─────────────────────────────────────────────────────
        private void ConfigurarFormulario()
        {
            Text = "Weezo";
            Size = new Size(560, 752);
            FormBorderStyle = FormBorderStyle.None;
            BackColor = CFondo;
            StartPosition = FormStartPosition.CenterScreen;
            ForeColor = CTexto;
            Font = new Font("Segoe UI", 9);

            // ── Barra superior ───────────────────────────────
            var panelTop = new Panel
            {
                Size = new Size(560, 64),
                Location = new Point(0, 0),
                BackColor = CTopBar
            };
            panelTop.MouseDown += IniciarArrastre;
            panelTop.MouseMove += Arrastrar;

            var lblRayo = new Label
            {
                Text = "⚡",
                ForeColor = CBlurple,
                Font = new Font("Segoe UI", 17, FontStyle.Bold),
                Location = new Point(22, 15),
                AutoSize = true
            };
            lblRayo.MouseDown += IniciarArrastre;
            lblRayo.MouseMove += Arrastrar;

            var lblTitulo = new Label
            {
                Text = "Weezo Dashboard",
                ForeColor = CTexto,
                Font = new Font("Segoe UI", 15, FontStyle.Bold),
                Location = new Point(75, 18),
                AutoSize = true
            };
            lblTitulo.MouseDown += IniciarArrastre;
            lblTitulo.MouseMove += Arrastrar;

            var btnCerrar = new Button
            {
                Text = "✕",
                Location = new Point(514, 16),
                Size = new Size(32, 32),
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.FromArgb(200, 200, 200),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            btnCerrar.FlatAppearance.BorderSize = 0;
            btnCerrar.FlatAppearance.MouseOverBackColor = Color.FromArgb(232, 17, 35);
            btnCerrar.Click += (s, e) => Application.Exit();

            var btnMin = new Button
            {
                Text = "—",
                Location = new Point(476, 16),
                Size = new Size(32, 32),
                BackColor = Color.FromArgb(45, 45, 48),
                ForeColor = Color.FromArgb(200, 200, 200),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            btnMin.FlatAppearance.BorderSize = 0;
            btnMin.FlatAppearance.MouseOverBackColor = Color.FromArgb(70, 70, 74);
            btnMin.Click += (s, e) => WindowState = FormWindowState.Minimized;

            panelTop.Controls.AddRange(new Control[] { lblRayo, lblTitulo, btnMin, btnCerrar });

            // ── Ventana activa ───────────────────────────────
            var lblHeader = new Label
            {
                Text = "VENTANA ACTIVA",
                ForeColor = CTextoSec,
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                Location = new Point(24, 80),
                AutoSize = true
            };

            lblVentanaActual = new Label
            {
                Text = "—",
                ForeColor = CTexto,
                Font = new Font("Segoe UI Semibold", 11, FontStyle.Bold),
                Location = new Point(24, 98),
                Size = new Size(512, 24),
                AutoEllipsis = true
            };

            // ── Botones de acción ────────────────────────────
            var btnRefrescar = CrearBotonAccion("↻  Refrescar", new Point(24, 132), 150, CBlurple);
            btnRefrescar.Click += (s, e) => CargarGrafico();

            var btnWeezo = CrearBotonAccion("♥  Pedir opinión", new Point(184, 132), 160, CBlurple2);
            btnWeezo.Click += async (s, e) =>
            {
                btnWeezo.Enabled = false;
                btnWeezo.Text = "Cargando...";
                string respuesta = await EjecutarCerebroAsync();
                btnWeezo.Enabled = true;
                btnWeezo.Text = "  Pedir opinión";
                if (!string.IsNullOrWhiteSpace(respuesta))
                {
                    MessageBox.Show(respuesta, "Weezo dice", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    MostrarNotificacion(respuesta);
                }
            };

            var btnConfig = CrearBotonAccion("⚙", new Point(354, 132), 44, Color.FromArgb(50, 50, 54));
            btnConfig.Font = new Font("Segoe UI", 13);
            btnConfig.Click += (s, e) =>
            {
                var f = new FormConfig();
                if (f.ShowDialog() == DialogResult.OK) ActualizarIntervaloTimer();
            };

            var btnStats = CrearBotonAccion("📊", new Point(408, 132), 44, CTeal);
            btnStats.Font = new Font("Segoe UI", 13);
            btnStats.Click += (s, e) => new FormEstadisticas().ShowDialog();

            // ── Tarjeta del gráfico ──────────────────────────
            var cardChart = new Panel
            {
                Location = new Point(24, 192),
                Size = new Size(512, 410),
                BackColor = CTarjeta
            };
            chartActividad = new PictureBox
            {
                Location = new Point(0, 0),
                Size = new Size(512, 410),
                BackColor = CTarjeta
            };
            cardChart.Controls.Add(chartActividad);

            // ── Tarjeta de meta ──────────────────────────────
            var cardMeta = new Panel
            {
                Location = new Point(24, 618),
                Size = new Size(512, 104),
                BackColor = CTarjeta
            };

            var lblMeta = new Label
            {
                Name = "lblMeta",
                Text = "Meta diaria: cargando...",
                ForeColor = CBlurple,
                Font = new Font("Segoe UI Semibold", 10, FontStyle.Bold),
                Location = new Point(18, 16),
                AutoSize = true
            };

            var progressMeta = new ProgressBar
            {
                Name = "progressMeta",
                Location = new Point(18, 46),
                Size = new Size(476, 18),
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Style = ProgressBarStyle.Continuous
            };

            var lblPorcentaje = new Label
            {
                Name = "lblPorcentaje",
                Text = "0% completado",
                ForeColor = CTextoSec,
                Font = new Font("Segoe UI", 9),
                Location = new Point(18, 72),
                AutoSize = true
            };

            cardMeta.Controls.AddRange(new Control[] { lblMeta, progressMeta, lblPorcentaje });

            Controls.AddRange(new Control[]
            {
                panelTop, lblHeader, lblVentanaActual,
                btnRefrescar, btnWeezo, btnConfig, btnStats,
                cardChart, cardMeta
            });

            // Redondear elementos (después de agregarlos para tener su tamaño)
            RedondearControl(btnRefrescar, 10);
            RedondearControl(btnWeezo, 10);
            RedondearControl(btnConfig, 10);
            RedondearControl(btnStats, 10);
            RedondearControl(btnMin, 8);
            RedondearControl(btnCerrar, 8);
            RedondearControl(cardChart, 14);
            RedondearControl(cardMeta, 14);
        }

        private Button CrearBotonAccion(string texto, Point pos, int ancho, Color color)
        {
            var btn = new Button
            {
                Text = texto,
                Location = pos,
                Size = new Size(ancho, 40),
                BackColor = color,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 9, FontStyle.Bold),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        // ─────────────────────────────────────────────────────
        // SYSTEM TRAY
        // ─────────────────────────────────────────────────────
        private void ConfigurarTray()
        {
            string rutaIco = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "weezo.ico");
            _trayIcon = new NotifyIcon
            {
                Icon = System.IO.File.Exists(rutaIco) ? new Icon(rutaIco) : SystemIcons.Application,
                Text = "Weezo — Activo",
                Visible = true
            };

            var menu = new ContextMenuStrip();
            var itemMostrar = new ToolStripMenuItem("Mostrar Dashboard");
            itemMostrar.Click += (s, e) => { Show(); WindowState = FormWindowState.Normal; BringToFront(); };
            var itemSalir = new ToolStripMenuItem("Salir");
            itemSalir.Click += (s, e) => Application.Exit();
            menu.Items.Add(itemMostrar);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(itemSalir);
            _trayIcon.ContextMenuStrip = menu;

            _trayIcon.DoubleClick += (s, e) => { Show(); WindowState = FormWindowState.Normal; BringToFront(); };

            Resize += (s, e) =>
            {
                if (WindowState == FormWindowState.Minimized)
                {
                    Hide();
                    _trayIcon.ShowBalloonTip(2000, "Weezo sigue activo",
                        "Estoy en la bandeja del sistema.", ToolTipIcon.Info);
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
            _weezTimer.Interval = 900000; // 15 min por defecto
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
                using var cmd = new SqlCommand("INSERT INTO LogsActividad (VentanaTitulo) VALUES (@t)", conn);
                cmd.Parameters.AddWithValue("@t", titulo);
                cmd.ExecuteNonQuery();
            }
            catch { }
        }

        // ─────────────────────────────────────────────────────
        // CEREBRO.EXE
        // ─────────────────────────────────────────────────────
        private async System.Threading.Tasks.Task<string> EjecutarCerebroAsync()
        {
            try
            {
                if (!System.IO.File.Exists(RutaExe))
                {
                    MessageBox.Show("No se encontró el módulo de IA (cerebro.exe).",
                        "Weezo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return string.Empty;
                }

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = RutaExe,
                    Arguments = "",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                };

                using var proceso = new System.Diagnostics.Process { StartInfo = psi };
                proceso.Start();
                string salida = await proceso.StandardOutput.ReadToEndAsync();
                string errores = await proceso.StandardError.ReadToEndAsync();
                await proceso.WaitForExitAsync();

                if (!string.IsNullOrWhiteSpace(salida)) return salida.Trim();
                if (!string.IsNullOrWhiteSpace(errores)) return "Weezo tuvo un problema técnico. Intenta de nuevo en un momento.";
                return string.Empty;
            }
            catch
            {
                return "No se pudo contactar a Weezo en este momento.";
            }
        }

        private void MostrarNotificacion(string mensaje)
        {
            string textoCorto = mensaje.Length > 200 ? mensaje[..200] + "..." : mensaje;
            _trayIcon.ShowBalloonTip(8000, "Weezo dice", textoCorto, ToolTipIcon.None);
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
                g.Clear(CTarjeta);

                if (datos.Count == 0)
                {
                    using var f = new Font("Segoe UI", 10);
                    using var bg = new SolidBrush(CTextoSec);
                    g.DrawString("Sin datos aún — espera un momento...", f, bg, new PointF(120, 175));
                    chartActividad.Image = bmp;
                    return;
                }

                Color[] colores =
                {
                    CBlurple,
                    Color.FromArgb(255, 107, 107),
                    CTeal,
                    Color.FromArgb(255, 195, 0),
                    Color.FromArgb(149, 117, 205)
                };

                int total = 0;
                var keys = new List<string>(datos.Keys);
                var values = new List<int>(datos.Values);
                foreach (var v in values) total += v;

                var rectDona = new Rectangle(151, 24, 210, 210);
                var rectHueco = new Rectangle(196, 69, 120, 120);
                float angulo = -90f;

                for (int i = 0; i < keys.Count; i++)
                {
                    float sweep = values[i] / (float)total * 360f;
                    using var br = new SolidBrush(colores[i % colores.Length]);
                    g.FillPie(br, rectDona, angulo, sweep);
                    angulo += sweep;
                }

                using var brHueco = new SolidBrush(CTarjeta);
                g.FillEllipse(brHueco, rectHueco);

                string topCat = keys[0];
                float topPct = values[0] / (float)total * 100f;
                using var fTop = new Font("Segoe UI", 13, FontStyle.Bold);
                using var fPct = new Font("Segoe UI", 9);
                using var brW = new SolidBrush(CTexto);
                using var brG = new SolidBrush(CTextoSec);
                var sf = new StringFormat { Alignment = StringAlignment.Center };

                g.DrawString($"{topPct:F0}%", fTop, brW, new RectangleF(rectHueco.X, rectHueco.Y + 42, rectHueco.Width, 25), sf);
                g.DrawString(topCat, fPct, brG, new RectangleF(rectHueco.X, rectHueco.Y + 68, rectHueco.Width, 20), sf);

                using var fLeg = new Font("Segoe UI", 10);
                int ly = 258;
                for (int i = 0; i < keys.Count; i++)
                {
                    using var brColor = new SolidBrush(colores[i % colores.Length]);
                    using var path = new GraphicsPath();
                    path.AddEllipse(40, ly, 14, 14);
                    g.FillPath(brColor, path);
                    float pct = values[i] / (float)total * 100f;
                    int min = values[i] / 60;
                    string tiempo = min >= 1 ? $"{min}m {values[i] % 60}s" : $"{values[i]}s";
                    g.DrawString($"{keys[i]}    {pct:F1}%    ({tiempo})", fLeg, brW, 64, ly - 2);
                    ly += 26;
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
                        WHEN VentanaTitulo LIKE '%Visual Studio%' OR VentanaTitulo LIKE '%VS Code%'
                          OR VentanaTitulo LIKE '%SSMS%' OR VentanaTitulo LIKE '%sql%'  THEN 'Programando'
                        WHEN VentanaTitulo LIKE '%Valorant%' OR VentanaTitulo LIKE '%Overwatch%'
                          OR VentanaTitulo LIKE '%Steam%' OR VentanaTitulo LIKE '%Epic Games%'
                          OR VentanaTitulo LIKE '%Wuthering%' OR VentanaTitulo LIKE '%Street Fighter%' THEN 'Jugando'
                        WHEN VentanaTitulo LIKE '%Chrome%' OR VentanaTitulo LIKE '%Firefox%'
                          OR VentanaTitulo LIKE '%Brave%' OR VentanaTitulo LIKE '%Edge%'  THEN 'Navegando'
                        WHEN VentanaTitulo LIKE '%YouTube%' OR VentanaTitulo LIKE '%Instagram%'
                          OR VentanaTitulo LIKE '%Twitter%' OR VentanaTitulo LIKE '%TikTok%'
                          OR VentanaTitulo LIKE '%Discord%'  THEN 'Redes/Ocio'
                        ELSE 'Otros'
                    END AS Categoria,
                    COUNT(*) AS Total
                FROM LogsActividad
                WHERE FechaHora >= DATEADD(HOUR, -1, SYSUTCDATETIME())
                GROUP BY
                    CASE
                        WHEN VentanaTitulo LIKE '%Visual Studio%' OR VentanaTitulo LIKE '%VS Code%'
                          OR VentanaTitulo LIKE '%SSMS%' OR VentanaTitulo LIKE '%sql%'  THEN 'Programando'
                        WHEN VentanaTitulo LIKE '%Valorant%' OR VentanaTitulo LIKE '%Overwatch%'
                          OR VentanaTitulo LIKE '%Steam%' OR VentanaTitulo LIKE '%Epic Games%'
                          OR VentanaTitulo LIKE '%Wuthering%' OR VentanaTitulo LIKE '%Street Fighter%' THEN 'Jugando'
                        WHEN VentanaTitulo LIKE '%Chrome%' OR VentanaTitulo LIKE '%Firefox%'
                          OR VentanaTitulo LIKE '%Brave%' OR VentanaTitulo LIKE '%Edge%'  THEN 'Navegando'
                        WHEN VentanaTitulo LIKE '%YouTube%' OR VentanaTitulo LIKE '%Instagram%'
                          OR VentanaTitulo LIKE '%Twitter%' OR VentanaTitulo LIKE '%TikTok%'
                          OR VentanaTitulo LIKE '%Discord%'  THEN 'Redes/Ocio'
                        ELSE 'Otros'
                    END
                ORDER BY Total DESC";

            using var cmd = new SqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                resultado[reader.GetString(0)] = reader.GetInt32(1);
            return resultado;
        }

        // ─────────────────────────────────────────────────────
        // METAS Y CONFIG
        // ─────────────────────────────────────────────────────
        private string ObtenerConfig(string clave, string defecto = "")
        {
            try
            {
                using var conn = new SqlConnection(ConnStr);
                conn.Open();
                using var cmd = new SqlCommand("SELECT Valor FROM Configuracion WHERE Clave = @k", conn);
                cmd.Parameters.AddWithValue("@k", clave);
                var result = cmd.ExecuteScalar();
                return result?.ToString() ?? defecto;
            }
            catch { return defecto; }
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

        private void ActualizarIntervaloTimer()
        {
            int min = int.TryParse(ObtenerConfig("IntervaloNotificacion", "15"), out int m) ? m : 15;
            if (min < 10) min = 10;
            _weezTimer.Interval = min * 60 * 1000;
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

                string col = categoria switch
                {
                    "codigo" => "MinutosCodigo",
                    "juego" => "MinutosJuego",
                    _ => "MinutosOcio"
                };
                using var cmd = new SqlCommand(
                    $"UPDATE ProgresosDiarios SET {col} = {col} + 1 WHERE Fecha = CAST(GETDATE() AS DATE)", conn);
                cmd.ExecuteNonQuery();
            }
            catch { }
        }

        private void ActualizarBarraMeta()
        {
            try
            {
                int meta = int.TryParse(ObtenerConfig("MetaMinutosCodigo", "120"), out int m) ? m : 120;
                using var conn = new SqlConnection(ConnStr);
                conn.Open();
                using var cmd = new SqlCommand(@"
                    SELECT ISNULL(MinutosCodigo, 0) FROM ProgresosDiarios
                    WHERE Fecha = CAST(GETDATE() AS DATE)", conn);
                int seg = Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
                int mins = seg / 60;
                int pct = Math.Min(100, (int)(mins / (float)meta * 100));

                if (InvokeRequired) Invoke(() => RefrescarBarraMeta(mins, meta, pct));
                else RefrescarBarraMeta(mins, meta, pct);
            }
            catch { }
        }

        private void RefrescarBarraMeta(int minutos, int meta, int porcentaje)
        {
            string nombre = ObtenerConfig("NombreUsuario", "amigo");
            if (Controls.Find("progressMeta", true).FirstOrDefault() is ProgressBar pb) pb.Value = porcentaje;
            if (Controls.Find("lblMeta", true).FirstOrDefault() is Label lm)
                lm.Text = $"Meta de {nombre}:  {minutos} / {meta} min de código hoy";
            if (Controls.Find("lblPorcentaje", true).FirstOrDefault() is Label lp)
            {
                lp.Text = porcentaje >= 100 ? "✓ ¡Meta cumplida!" : $"{porcentaje}% completado";
                lp.ForeColor = porcentaje >= 100 ? CTeal : CTextoSec;
            }
        }

        // ─────────────────────────────────────────────────────
        // API KEY + BIENVENIDA
        // ─────────────────────────────────────────────────────
        private void MostrarSaludoInicial()
        {
            string nombre = ObtenerConfig("NombreUsuario", "amigo");
            var t = new System.Windows.Forms.Timer { Interval = 1500 };
            t.Tick += (s, e) =>
            {
                t.Stop();
                _trayIcon.ShowBalloonTip(4000, $"¡Bienvenido, {nombre}!",
                    "Weezo está activo y listo para acompañarte.", ToolTipIcon.Info);
            };
            t.Start();
        }

        private void PedirApiKey()
        {
            using var form = new Form
            {
                Text = "Weezo — Configurar API Key",
                Size = new Size(480, 290),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterScreen,
                BackColor = CFondo,
                ForeColor = CTexto,
                MaximizeBox = false,
                MinimizeBox = false
            };
            var lblInfo = new Label
            {
                Text = "Weezo necesita una API key de Groq para funcionar.\nEs gratis. Consíguela en el siguiente enlace:",
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
                LinkColor = CBlurple
            };
            linkGroq.LinkClicked += (s, e) => System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo { FileName = "https://console.groq.com/keys", UseShellExecute = true });
            var txtKey = new TextBox
            {
                Location = new Point(20, 115),
                Size = new Size(430, 30),
                BackColor = CTarjeta,
                ForeColor = CTexto,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 11),
                PlaceholderText = "Pega aquí tu API key (gsk_...)"
            };
            var btnGuardar = new Button
            {
                Text = "Guardar y continuar",
                Location = new Point(20, 170),
                Size = new Size(430, 44),
                BackColor = CBlurple,
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
                    MessageBox.Show("Por favor pega tu API key.", "Weezo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                GuardarConfig("ApiKey", key);
                form.DialogResult = DialogResult.OK; form.Close();
            };
            form.Controls.AddRange(new Control[] { lblInfo, linkGroq, txtKey, btnGuardar });
            form.ShowDialog();
        }

        // ─────────────────────────────────────────────────────
        // BASE DE DATOS (auto-creación)
        // ─────────────────────────────────────────────────────
        private void InicializarBaseDeDatos()
        {
            try
            {
                string connMaster =
                    "Server=(localdb)\\MSSQLLocalDB;Database=master;Integrated Security=true;TrustServerCertificate=true;";
                using var connM = new SqlConnection(connMaster);
                connM.Open();
                using var cmdC = new SqlCommand(
                    "IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'WeezoDB') CREATE DATABASE WeezoDB;", connM);
                cmdC.ExecuteNonQuery();
                connM.Close();

                using var conn = new SqlConnection(ConnStr);
                conn.Open();
                using var cmd = new SqlCommand(@"
                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LogsActividad')
                    BEGIN
                        CREATE TABLE LogsActividad (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            VentanaTitulo NVARCHAR(500) NOT NULL,
                            FechaHora DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME());
                        CREATE NONCLUSTERED INDEX IX_Logs_Fecha ON LogsActividad (FechaHora DESC);
                    END
                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Configuracion')
                    BEGIN
                        CREATE TABLE Configuracion (Clave NVARCHAR(50) PRIMARY KEY, Valor NVARCHAR(MAX) NOT NULL);
                        INSERT INTO Configuracion (Clave, Valor) VALUES
                        ('NombreUsuario','Usuario'),('MetaMinutosCodigo','120'),
                        ('IntervaloNotificacion','15'),('ModoHabla','normal'),
                        ('Idioma','es'),('ModoNoMolestar','0'),('PrimeraVez','1'),('ApiKey','');
                    END
                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ProgresosDiarios')
                    BEGIN
                        CREATE TABLE ProgresosDiarios (
                            Id INT IDENTITY(1,1) PRIMARY KEY,
                            Fecha DATE NOT NULL DEFAULT CAST(GETDATE() AS DATE),
                            MinutosCodigo INT NOT NULL DEFAULT 0,
                            MinutosJuego INT NOT NULL DEFAULT 0,
                            MinutosOcio INT NOT NULL DEFAULT 0,
                            PuntosGanados INT NOT NULL DEFAULT 0,
                            RachaActual INT NOT NULL DEFAULT 0);
                    END", conn);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error iniciando la base de datos:\n\n{ex.Message}\n\nAsegúrate de tener SQL Server LocalDB instalado.",
                    "Weezo — Setup", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
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

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            base.OnFormClosing(e);
        }
    }
}