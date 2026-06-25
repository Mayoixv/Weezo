using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace Weezo
{
    public class FormEstadisticas : Form
    {
        private const string ConnStr =
            "Server=(localdb)\\MSSQLLocalDB;" +
            "Database=WeezoDB;" +
            "Integrated Security=true;" +
            "TrustServerCertificate=true;";

        private PictureBox chartSemanal = null!;
        private PictureBox chartTopApps = null!;
        private Point _dragOffset;

        public FormEstadisticas()
        {
            ConfigurarUI();
            CargarDatos();
        }

        private void ConfigurarUI()
        {
            Text = "Weezo — Estadísticas";
            Size = new Size(600, 720);
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Color.FromArgb(30, 30, 30);
            StartPosition = FormStartPosition.CenterParent;
            ForeColor = Color.White;

            // Panel superior
            var panelTop = new Panel
            {
                Size = new Size(600, 55),
                Location = new Point(0, 0),
                BackColor = Color.FromArgb(20, 20, 20)
            };
            panelTop.MouseDown += IniciarArrastre;
            panelTop.MouseMove += Arrastrar;

            var lblTitulo = new Label
            {
                Text = "📊 Estadísticas de Weezo",
                ForeColor = Color.FromArgb(114, 137, 218),
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                Location = new Point(16, 12),
                AutoSize = true
            };
            lblTitulo.MouseDown += IniciarArrastre;
            lblTitulo.MouseMove += Arrastrar;

            var btnCerrar = new Button
            {
                Text = "✕",
                Location = new Point(552, 10),
                Size = new Size(36, 36),
                BackColor = Color.FromArgb(220, 53, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 11, FontStyle.Bold)
            };
            btnCerrar.FlatAppearance.BorderSize = 0;
            btnCerrar.Click += (s, e) => Close();
            panelTop.Controls.AddRange(new Control[] { lblTitulo, btnCerrar });

            // Sección historial semanal
            var lblSemanal = new Label
            {
                Text = "📅 Actividad — Últimos 7 días",
                ForeColor = Color.FromArgb(114, 137, 218),
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Location = new Point(20, 70),
                AutoSize = true
            };

            chartSemanal = new PictureBox
            {
                Location = new Point(10, 95),
                Size = new Size(575, 220),
                BackColor = Color.FromArgb(40, 40, 40)
            };

            // Separador
            var separador = new Panel
            {
                Location = new Point(10, 325),
                Size = new Size(575, 2),
                BackColor = Color.FromArgb(60, 60, 60)
            };

            // Sección top apps
            var lblTopApps = new Label
            {
                Text = "🏆 Top Aplicaciones — Todo el tiempo",
                ForeColor = Color.FromArgb(114, 137, 218),
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Location = new Point(20, 338),
                AutoSize = true
            };

            chartTopApps = new PictureBox
            {
                Location = new Point(10, 363),
                Size = new Size(575, 320),
                BackColor = Color.FromArgb(40, 40, 40)
            };

            // Botón refrescar
            var btnRefrescar = new Button
            {
                Text = "↻  Refrescar",
                Location = new Point(450, 65),
                Size = new Size(130, 30),
                BackColor = Color.FromArgb(114, 137, 218),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnRefrescar.FlatAppearance.BorderSize = 0;
            btnRefrescar.Click += (s, e) => CargarDatos();

            Controls.AddRange(new Control[]
            {
                panelTop, lblSemanal, chartSemanal,
                separador, lblTopApps, chartTopApps, btnRefrescar
            });
        }

        // ─────────────────────────────────────────────────────
        // CARGAR DATOS
        // ─────────────────────────────────────────────────────
        private void CargarDatos()
        {
            DibujarHistorialSemanal();
            DibujarTopApps();
        }

        // ─────────────────────────────────────────────────────
        // GRÁFICO DE BARRAS — HISTORIAL SEMANAL
        // ─────────────────────────────────────────────────────
        private void DibujarHistorialSemanal()
        {
            var datos = ObtenerHistorialSemanal();
            var bmp = new Bitmap(chartSemanal.Width, chartSemanal.Height);
            using var g = Graphics.FromImage(bmp);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.FromArgb(40, 40, 40));

            if (datos.Count == 0)
            {
                using var f = new Font("Segoe UI", 10);
                g.DrawString("Sin datos todavía...", f, Brushes.Gray, new PointF(200, 90));
                chartSemanal.Image = bmp;
                return;
            }

            int padLeft = 55;
            int padBot = 40;
            int padTop = 20;
            int w = bmp.Width - padLeft - 20;
            int h = bmp.Height - padBot - padTop;
            int maxSeg = datos.Values.Max();
            int barW = w / datos.Count - 10;

            using var penGrid = new Pen(Color.FromArgb(55, 55, 55), 1);
            using var brBlurple = new SolidBrush(Color.FromArgb(114, 137, 218));
            using var brText = new SolidBrush(Color.FromArgb(180, 180, 180));
            using var fSmall = new Font("Segoe UI", 8);
            using var fVal = new Font("Segoe UI", 8, FontStyle.Bold);
            using var brWhite = new SolidBrush(Color.White);

            // Líneas de cuadrícula
            for (int i = 0; i <= 4; i++)
            {
                int y = padTop + (h / 4 * i);
                g.DrawLine(penGrid, padLeft, y, padLeft + w, y);
                int val = maxSeg - (maxSeg / 4 * i);
                g.DrawString($"{val / 60}m", fSmall, brText, 2, y - 7);
            }

            // Barras
            var keys = datos.Keys.ToList();
            var values = datos.Values.ToList();

            for (int i = 0; i < keys.Count; i++)
            {
                int barH = maxSeg == 0 ? 0 : (int)(values[i] / (float)maxSeg * h);
                int x = padLeft + i * (barW + 10) + 5;
                int y = padTop + h - barH;

                // Barra con gradiente
                var rect = new Rectangle(x, y, barW, barH);
                if (barH > 0)
                {
                    using var gradBrush = new LinearGradientBrush(
                        rect,
                        Color.FromArgb(114, 137, 218),
                        Color.FromArgb(88, 101, 242),
                        LinearGradientMode.Vertical);
                    g.FillRectangle(gradBrush, rect);
                }

                // Valor encima
                string valStr = values[i] >= 60
                    ? $"{values[i] / 60}m"
                    : $"{values[i]}s";
                g.DrawString(valStr, fVal, brWhite, x, y - 16);

                // Día debajo
                string dia = keys[i].ToString("ddd dd", new System.Globalization.CultureInfo("es-DO"));
                g.DrawString(dia, fSmall, brText, x - 2, padTop + h + 5);
            }

            chartSemanal.Image = bmp;
        }

        // ─────────────────────────────────────────────────────
        // GRÁFICO DE BARRAS HORIZONTAL — TOP APPS
        // ─────────────────────────────────────────────────────
        private void DibujarTopApps()
        {
            var datos = ObtenerTopApps();
            var bmp = new Bitmap(chartTopApps.Width, chartTopApps.Height);
            using var g = Graphics.FromImage(bmp);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.FromArgb(40, 40, 40));

            if (datos.Count == 0)
            {
                using var f = new Font("Segoe UI", 10);
                g.DrawString("Sin datos todavía...", f, Brushes.Gray, new PointF(200, 140));
                chartTopApps.Image = bmp;
                return;
            }

            Color[] colores =
            {
                Color.FromArgb(114, 137, 218),
                Color.FromArgb(255, 107, 107),
                Color.FromArgb(78,  205, 196),
                Color.FromArgb(255, 195,   0),
                Color.FromArgb(149, 117, 205),
                Color.FromArgb(255, 159,  64),
                Color.FromArgb( 75, 192, 192),
                Color.FromArgb(255,  99, 132),
                Color.FromArgb(54,  162, 235),
                Color.FromArgb(153, 102, 255)
            };

            int maxSeg = datos.Values.Max();
            int padLeft = 180;
            int padR = 80;
            int barH = 24;
            int gap = 12;
            int startY = 15;

            using var brWhite = new SolidBrush(Color.White);
            using var brGray = new SolidBrush(Color.FromArgb(160, 160, 160));
            using var fApp = new Font("Segoe UI", 9);
            using var fVal = new Font("Segoe UI", 9, FontStyle.Bold);

            var keys = datos.Keys.ToList();
            var values = datos.Values.ToList();
            int totalW = bmp.Width - padLeft - padR;

            for (int i = 0; i < keys.Count; i++)
            {
                int y = startY + i * (barH + gap);
                int barW = maxSeg == 0 ? 0 : (int)(values[i] / (float)maxSeg * totalW);

                // Nombre de la app (truncado)
                string nombre = keys[i].Length > 28
                    ? keys[i][..28] + "…"
                    : keys[i];
                g.DrawString(nombre, fApp, brGray, 10, y + 4);

                // Barra
                using var br = new SolidBrush(colores[i % colores.Length]);
                if (barW > 0)
                    g.FillRectangle(br, padLeft, y, barW, barH);

                // Fondo gris de la barra completa
                using var brBg = new SolidBrush(Color.FromArgb(55, 55, 55));
                g.FillRectangle(brBg, padLeft + barW, y, totalW - barW, barH);

                // Valor a la derecha
                string tiempo = values[i] >= 3600
                    ? $"{values[i] / 3600}h {values[i] % 3600 / 60}m"
                    : values[i] >= 60
                        ? $"{values[i] / 60}m"
                        : $"{values[i]}s";
                g.DrawString(tiempo, fVal, brWhite,
                    padLeft + totalW + 5, y + 4);
            }

            chartTopApps.Image = bmp;
        }

        // ─────────────────────────────────────────────────────
        // QUERIES SQL
        // ─────────────────────────────────────────────────────
        private Dictionary<DateTime, int> ObtenerHistorialSemanal()
        {
            var resultado = new Dictionary<DateTime, int>();
            try
            {
                using var conn = new SqlConnection(ConnStr);
                conn.Open();
                using var cmd = new SqlCommand(@"
                    SELECT 
                        CAST(FechaHora AS DATE) AS Dia,
                        COUNT(*) AS TotalSegundos
                    FROM LogsActividad
                    WHERE FechaHora >= DATEADD(DAY, -6, CAST(GETDATE() AS DATE))
                    GROUP BY CAST(FechaHora AS DATE)
                    ORDER BY Dia ASC", conn);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    resultado[reader.GetDateTime(0)] = reader.GetInt32(1);
            }
            catch { }
            return resultado;
        }

        private Dictionary<string, int> ObtenerTopApps()
        {
            var resultado = new Dictionary<string, int>();
            try
            {
                using var conn = new SqlConnection(ConnStr);
                conn.Open();
                using var cmd = new SqlCommand(@"
                    SELECT TOP 10
                        CASE
                            WHEN VentanaTitulo LIKE '%Visual Studio%' THEN 'Visual Studio'
                            WHEN VentanaTitulo LIKE '%VS Code%'       THEN 'VS Code'
                            WHEN VentanaTitulo LIKE '%SSMS%'          THEN 'SSMS'
                            WHEN VentanaTitulo LIKE '%Chrome%'        THEN 'Google Chrome'
                            WHEN VentanaTitulo LIKE '%Firefox%'       THEN 'Firefox'
                            WHEN VentanaTitulo LIKE '%Edge%'          THEN 'Microsoft Edge'
                            WHEN VentanaTitulo LIKE '%Valorant%'      THEN 'Valorant'
                            WHEN VentanaTitulo LIKE '%Steam%'         THEN 'Steam'
                            WHEN VentanaTitulo LIKE '%Overwatch%'     THEN 'Overwatch 2'
                            WHEN VentanaTitulo LIKE '%Spotify%'       THEN 'Spotify'
                            WHEN VentanaTitulo LIKE '%Discord%'       THEN 'Discord'
                            WHEN VentanaTitulo LIKE '%WhatsApp%'      THEN 'WhatsApp'
                            WHEN VentanaTitulo LIKE '%YouTube%'       THEN 'YouTube'
                            WHEN VentanaTitulo LIKE '%Instagram%'     THEN 'Instagram'
                            WHEN VentanaTitulo LIKE '%Brave%'         THEN 'Brave Browser'
                            WHEN VentanaTitulo LIKE '%Word%'          THEN 'Microsoft Word'
                            WHEN VentanaTitulo LIKE '%Excel%'         THEN 'Microsoft Excel'
                            WHEN VentanaTitulo LIKE '%PowerPoint%'    THEN 'PowerPoint'
                            ELSE LEFT(VentanaTitulo, 40)
                        END AS App,
                        COUNT(*) AS Segundos
                    FROM LogsActividad
                    WHERE VentanaTitulo NOT LIKE '%Weezo%'
                      AND VentanaTitulo != ''
                    GROUP BY
                        CASE
                            WHEN VentanaTitulo LIKE '%Visual Studio%' THEN 'Visual Studio'
                            WHEN VentanaTitulo LIKE '%VS Code%'       THEN 'VS Code'
                            WHEN VentanaTitulo LIKE '%SSMS%'          THEN 'SSMS'
                            WHEN VentanaTitulo LIKE '%Chrome%'        THEN 'Google Chrome'
                            WHEN VentanaTitulo LIKE '%Firefox%'       THEN 'Firefox'
                            WHEN VentanaTitulo LIKE '%Edge%'          THEN 'Microsoft Edge'
                            WHEN VentanaTitulo LIKE '%Valorant%'      THEN 'Valorant'
                            WHEN VentanaTitulo LIKE '%Steam%'         THEN 'Steam'
                            WHEN VentanaTitulo LIKE '%Overwatch%'     THEN 'Overwatch 2'
                            WHEN VentanaTitulo LIKE '%Spotify%'       THEN 'Spotify'
                            WHEN VentanaTitulo LIKE '%Discord%'       THEN 'Discord'
                            WHEN VentanaTitulo LIKE '%WhatsApp%'      THEN 'WhatsApp'
                            WHEN VentanaTitulo LIKE '%YouTube%'       THEN 'YouTube'
                            WHEN VentanaTitulo LIKE '%Instagram%'     THEN 'Instagram'
                            WHEN VentanaTitulo LIKE '%Brave%'         THEN 'Brave Browser'
                            WHEN VentanaTitulo LIKE '%Word%'          THEN 'Microsoft Word'
                            WHEN VentanaTitulo LIKE '%Excel%'         THEN 'Microsoft Excel'
                            WHEN VentanaTitulo LIKE '%PowerPoint%'    THEN 'PowerPoint'
                            ELSE LEFT(VentanaTitulo, 40)
                        END
                    ORDER BY Segundos DESC", conn);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    resultado[reader.GetString(0)] = reader.GetInt32(1);
            }
            catch { }
            return resultado;
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
    }
}