using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace UniversityChecker
{
    // Provided GeoMath class
    internal static class GeoMath
    {
        public static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            double R = 6371;
            double dLat = ToRadians(lat2 - lat1);
            double dLon = ToRadians(lon2 - lon1);
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }
        private static double ToRadians(double angle) => Math.PI * angle / 180.0;
    }

    public class MainForm : Form
    {
        // --- UI Elements ---
        private ListBox lstHosts = null!;

        private Label lblHost1Name = null!;
        private TextBox txtHost1Ip = null!;
        private Label lblHost1Coords = null!;

        private Label lblHost2Name = null!;
        private TextBox txtHost2Ip = null!;
        private Label lblHost2Coords = null!;

        private Label lblDistance = null!;
        private Button btnClear = null!;
        private Button btnCalculate = null!;

        // --- Logic Data ---
        private static readonly HttpClient httpClient = new HttpClient();

        // Map fake host names to real domains for real IP resolution
        private readonly Dictionary<string, string> hostMappings = new()
        {
            { "host1", "ox.ac.uk" },
            { "host2", "mit.edu" },
            { "host3", "u-tokyo.ac.jp" },
            { "host4", "nasa.gov" }
        };

        private class HostData
        {
            public string Name { get; set; } = "";
            public string Ip { get; set; } = "";
            public double Lat { get; set; }
            public double Lon { get; set; }
            public bool IsValid { get; set; } = false;
        }

        private HostData _slot1 = new();
        private HostData _slot2 = new();
        private bool _isProcessing = false;

        public MainForm()
        {
            InitializeUI();
        }

        private void InitializeUI()
        {
            this.Text = "University Checker - Host Distance";
            this.Size = new Size(650, 400);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.BackColor = Color.White;

            Font monoFont = new Font("Consolas", 11, FontStyle.Bold);
            Font monoFontRegular = new Font("Consolas", 11, FontStyle.Regular);

            // --- ListBox ---
            lstHosts = new ListBox
            {
                Location = new Point(30, 30),
                Size = new Size(160, 280),
                Font = monoFontRegular
            };
            lstHosts.Items.AddRange(new[] { "host1", "host2", "host3", "host4" });
            lstHosts.SelectedIndexChanged += LstHosts_SelectedIndexChanged;

            // --- Slot 1 ---
            lblHost1Name = new Label { Text = "host1", Location = new Point(220, 30), AutoSize = true, Font = monoFont };
            txtHost1Ip = new TextBox { Location = new Point(220, 55), Width = 140, ReadOnly = true, Font = monoFontRegular, BorderStyle = BorderStyle.FixedSingle };
            lblHost1Coords = new Label { Text = "lat: -, \nlong: -", Location = new Point(380, 50), AutoSize = true, Font = monoFontRegular };

            // --- Slot 2 ---
            lblHost2Name = new Label { Text = "host2", Location = new Point(220, 100), AutoSize = true, Font = monoFont };
            txtHost2Ip = new TextBox { Location = new Point(220, 125), Width = 140, ReadOnly = true, Font = monoFontRegular, BorderStyle = BorderStyle.FixedSingle };
            lblHost2Coords = new Label { Text = "lat: -, \nlong: -", Location = new Point(380, 120), AutoSize = true, Font = monoFontRegular };

            // --- Distance & Buttons ---
            lblDistance = new Label
            {
                Text = "Distance = -",
                Location = new Point(220, 190),
                AutoSize = true,
                Font = monoFont
            };

            btnClear = new Button
            {
                Text = "Clear",
                Location = new Point(220, 240),
                Size = new Size(80, 30),
                Font = monoFont,
                FlatStyle = FlatStyle.Flat
            };
            btnClear.Click += BtnClear_Click;

            btnCalculate = new Button
            {
                Text = "Calculate",
                Location = new Point(320, 240),
                Size = new Size(100, 30),
                Font = monoFont,
                FlatStyle = FlatStyle.Flat
            };
            btnCalculate.Click += BtnCalculate_Click;

            // Add to Form
            this.Controls.Add(lstHosts);
            this.Controls.Add(lblHost1Name);
            this.Controls.Add(txtHost1Ip);
            this.Controls.Add(lblHost1Coords);
            this.Controls.Add(lblHost2Name);
            this.Controls.Add(txtHost2Ip);
            this.Controls.Add(lblHost2Coords);
            this.Controls.Add(lblDistance);
            this.Controls.Add(btnClear);
            this.Controls.Add(btnCalculate);
        }

        private async void LstHosts_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_isProcessing || lstHosts.SelectedItem == null) return;

            string selectedHost = lstHosts.SelectedItem.ToString()!;

            // Prevent selecting the same host twice
            if (_slot1.Name == selectedHost && _slot1.IsValid) { lstHosts.ClearSelected(); return; }
            if (_slot2.Name == selectedHost && _slot2.IsValid) { lstHosts.ClearSelected(); return; }

            _isProcessing = true;
            string domain = hostMappings[selectedHost];

            try
            {
                // 1. Resolve IP Address
                IPAddress[] addresses = await Dns.GetHostAddressesAsync(domain);
                string ip = addresses[0].ToString();

                // 2. Get Geolocation from ip-api.com
                string json = await httpClient.GetStringAsync($"http://ip-api.com/json/{ip}");
                using JsonDocument doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                double lat = root.GetProperty("lat").GetDouble();
                double lon = root.GetProperty("lon").GetDouble();

                // 3. Fill the slots (If both full, overwrite slot 2)
                if (!_slot1.IsValid)
                {
                    _slot1 = new HostData { Name = selectedHost, Ip = ip, Lat = lat, Lon = lon, IsValid = true };
                    lblHost1Name.Text = selectedHost;
                    txtHost1Ip.Text = ip;
                    lblHost1Coords.Text = $"lat:  {lat.ToString("F4")},\nlong: {lon.ToString("F4")}";
                }
                else
                {
                    _slot2 = new HostData { Name = selectedHost, Ip = ip, Lat = lat, Lon = lon, IsValid = true };
                    lblHost2Name.Text = selectedHost;
                    txtHost2Ip.Text = ip;
                    lblHost2Coords.Text = $"lat:  {lat.ToString("F4")},\nlong: {lon.ToString("F4")}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error resolving {selectedHost}: {ex.Message}", "Error");
            }
            finally
            {
                _isProcessing = false;
                lstHosts.ClearSelected(); // Reset selection
            }
        }

        private void BtnCalculate_Click(object? sender, EventArgs e)
        {
            if (!_slot1.IsValid || !_slot2.IsValid)
            {
                MessageBox.Show("Please select two valid hosts first.", "Info");
                return;
            }

            // Use the provided GeoMath class
            double distance = GeoMath.CalculateDistance(_slot1.Lat, _slot1.Lon, _slot2.Lat, _slot2.Lon);
            lblDistance.Text = $"Distance = {Math.Round(distance)}km";
        }

        private void BtnClear_Click(object? sender, EventArgs e)
        {
            _slot1 = new HostData();
            _slot2 = new HostData();

            lblHost1Name.Text = "host1";
            txtHost1Ip.Clear();
            lblHost1Coords.Text = "lat: -, \nlong: -";

            lblHost2Name.Text = "host2";
            txtHost2Ip.Clear();
            lblHost2Coords.Text = "lat: -, \nlong: -";

            lblDistance.Text = "Distance = -";
        }
    }
}