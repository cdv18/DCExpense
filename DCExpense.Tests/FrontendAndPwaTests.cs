using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Xunit;

namespace DCExpense.Tests
{
    public class FrontendAndPwaTests
    {
        private static string GetWwwrootDirectory()
        {
            var current = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(current))
            {
                var candidate = Path.Combine(current, "DCExpense.WebApi", "wwwroot");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                // Check sibling DCExpense.WebApi folder from DCExpense.Tests
                var siblingCandidate = Path.Combine(current, "..", "DCExpense.WebApi", "wwwroot");
                if (Directory.Exists(siblingCandidate))
                {
                    return Path.GetFullPath(siblingCandidate);
                }

                var parent = Directory.GetParent(current);
                if (parent == null) break;
                current = parent.FullName;
            }

            var direct = @"D:\Agy\DCExpense\DCExpense.WebApi\wwwroot";
            if (Directory.Exists(direct)) return direct;

            throw new DirectoryNotFoundException("Could not locate DCExpense.WebApi/wwwroot directory.");
        }

        private static string GetWebApiProgramCsPath()
        {
            var current = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(current))
            {
                var candidate = Path.Combine(current, "DCExpense.WebApi", "Program.cs");
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                var parent = Directory.GetParent(current);
                if (parent == null) break;
                current = parent.FullName;
            }

            var direct = @"D:\Agy\DCExpense\DCExpense.WebApi\Program.cs";
            if (File.Exists(direct)) return direct;

            throw new FileNotFoundException("Could not locate DCExpense.WebApi/Program.cs file.");
        }

        [Theory]
        [InlineData("index.html")]
        [InlineData("app.js")]
        [InlineData("manifest.json")]
        [InlineData("sw.js")]
        public void Wwwroot_RequiredFrontendFiles_ShouldExistAndBeNonEmpty(string fileName)
        {
            var wwwrootDir = GetWwwrootDirectory();
            var filePath = Path.Combine(wwwrootDir, fileName);

            File.Exists(filePath).Should().BeTrue($"File '{fileName}' should exist in wwwroot.");
            var fileInfo = new FileInfo(filePath);
            fileInfo.Length.Should().BeGreaterThan(0, $"File '{fileName}' should not be empty.");
        }

        public class ManifestIconModel
        {
            [JsonPropertyName("src")]
            public string? Src { get; set; }

            [JsonPropertyName("sizes")]
            public string? Sizes { get; set; }

            [JsonPropertyName("type")]
            public string? Type { get; set; }
        }

        public class WebAppManifestModel
        {
            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("short_name")]
            public string? ShortName { get; set; }

            [JsonPropertyName("description")]
            public string? Description { get; set; }

            [JsonPropertyName("start_url")]
            public string? StartUrl { get; set; }

            [JsonPropertyName("display")]
            public string? Display { get; set; }

            [JsonPropertyName("background_color")]
            public string? BackgroundColor { get; set; }

            [JsonPropertyName("theme_color")]
            public string? ThemeColor { get; set; }

            [JsonPropertyName("icons")]
            public List<ManifestIconModel>? Icons { get; set; }
        }

        [Fact]
        public void ManifestJson_ShouldDeserializeSuccessfully_AndContainAllRequiredPwaAttributes()
        {
            var wwwrootDir = GetWwwrootDirectory();
            var manifestPath = Path.Combine(wwwrootDir, "manifest.json");
            var jsonContent = File.ReadAllText(manifestPath);

            var manifest = JsonSerializer.Deserialize<WebAppManifestModel>(jsonContent);

            manifest.Should().NotBeNull("manifest.json must be valid JSON");
            manifest!.Name.Should().NotBeNullOrWhiteSpace("PWA name is required");
            manifest.Name.Should().Be("DCExpense");

            manifest.ShortName.Should().NotBeNullOrWhiteSpace("PWA short_name is required");
            manifest.ShortName.Should().Be("DCExpense");

            manifest.StartUrl.Should().Be("/", "PWA start_url must be defined");
            manifest.Display.Should().Be("standalone", "display must be 'standalone' for PWA installation");
            manifest.ThemeColor.Should().NotBeNullOrWhiteSpace("theme_color must be specified for status bar styling");

            manifest.Icons.Should().NotBeNull("manifest.json must declare an icons array");
            manifest.Icons!.Count.Should().BeGreaterThanOrEqualTo(2, "manifest should contain standard 192x192 and 512x512 icons");

            manifest.Icons.Should().Contain(i => i.Sizes == "192x192" && i.Type == "image/png");
            manifest.Icons.Should().Contain(i => i.Sizes == "512x512" && i.Type == "image/png");

            foreach (var icon in manifest.Icons)
            {
                if (!string.IsNullOrEmpty(icon.Src))
                {
                    var iconPath = Path.Combine(wwwrootDir, icon.Src);
                    File.Exists(iconPath).Should().BeTrue($"Icon file '{icon.Src}' declared in manifest must exist on disk");
                }
            }
        }

        [Fact]
        public void IndexHtml_ShouldContainCoreDashboardContainers()
        {
            var wwwrootDir = GetWwwrootDirectory();
            var indexPath = Path.Combine(wwwrootDir, "index.html");
            var html = File.ReadAllText(indexPath);

            // 1. Dashboard View
            html.Should().Contain("id=\"view-dashboard\"", "Dashboard view container must be present");
            
            // 2. Safe-to-Spend metric
            html.Should().Contain("Safe-to-Spend", "Safe-to-Spend widget must be present");
            
            // 3. Net Worth metric
            html.Should().Contain("Net Worth", "Net Worth widget must be present");
            
            // 4. Budget Bars container
            html.Should().Contain("Ngân sách (50/30/20 & JARS)", "Budget bars component must be present");
            html.Should().Contain("Nhu cầu thiết yếu (Needs - 50%)");
            html.Should().Contain("Mong muốn (Wants - 30%)");
            html.Should().Contain("Tiết kiệm & Đầu tư (20%)");
            html.Should().Contain("Quỹ Giáo dục (EDU)");

            // 5. Chart Canvas
            html.Should().Contain("<canvas id=\"cashFlowChart\"", "Cash Flow chart canvas must be present");

            // 6. Reconciliation Staging View
            html.Should().Contain("id=\"view-reconciliation\"", "Reconciliation staging view must be present");
            html.Should().Contain("id=\"dropZone\"", "Reconciliation file drop zone must be present");
            html.Should().Contain("id=\"reconciliationTables\"", "Reconciliation tables container must be present");
            html.Should().Contain("Đã tự động hợp nhất (Auto-merged)");
            html.Should().Contain("Nghi vấn lệch ngày / Đảo số");
            html.Should().Contain("Giao dịch mới hoàn toàn");

            // 7. Split Transaction Modal
            html.Should().Contain("id=\"txModal\"", "Transaction modal must be present");
            html.Should().Contain("id=\"splitContainer\"", "Split items container must be present");
            html.Should().Contain("addSplitItem()", "Add split item action button must be wired");
            html.Should().Contain("id=\"txTotal\"", "Total amount element must be present");

            // 8. Dark Mode Toggle
            html.Should().Contain("id=\"themeToggle\"", "Theme toggle button must be present");
            html.Should().Contain("dark:", "Tailwind dark mode variant classes must be used");
        }

        [Fact]
        public void IndexHtml_ShouldLinkAllRequiredScriptsAndStylesheets()
        {
            var wwwrootDir = GetWwwrootDirectory();
            var indexPath = Path.Combine(wwwrootDir, "index.html");
            var html = File.ReadAllText(indexPath);

            // Tailwind CSS CDN
            html.Should().Contain("https://cdn.tailwindcss.com", "Tailwind CDN script must be linked");

            // Lucide Icons CDN
            html.Should().Contain("lucide", "Lucide icons CDN must be linked");

            // Chart.js CDN
            html.Should().Contain("chart.js", "Chart.js CDN must be linked");

            // manifest.json link tag
            html.Should().Contain("rel=\"manifest\"", "Manifest link tag must be present");
            html.Should().Contain("href=\"/manifest.json\"", "Manifest href must target /manifest.json");

            // app.js script tag
            html.Should().Contain("src=\"/app.js\"", "app.js script tag must be referenced");
        }

        [Fact]
        public void IndexHtml_NavigationLinks_ShouldContainAllCoreModules()
        {
            var wwwrootDir = GetWwwrootDirectory();
            var indexPath = Path.Combine(wwwrootDir, "index.html");
            var html = File.ReadAllText(indexPath);

            html.Should().Contain("switchView('dashboard')");
            html.Should().Contain("switchView('reconciliation')");
            html.Should().Contain("Sổ Giao Dịch");
            html.Should().Contain("Ngân Sách");
            html.Should().Contain("Báo Cáo");
        }

        [Fact]
        public void ServiceWorker_ShouldHaveValidCacheStructureAndLifecycleHandlers()
        {
            var wwwrootDir = GetWwwrootDirectory();
            var swPath = Path.Combine(wwwrootDir, "sw.js");
            var swJs = File.ReadAllText(swPath);

            // CACHE_NAME defined
            swJs.Should().Contain("CACHE_NAME", "Service worker should declare a CACHE_NAME");

            // Precache asset list defined
            swJs.Should().Contain("urlsToCache", "urlsToCache array must be defined");
            swJs.Should().Contain("'/index.html'");
            swJs.Should().Contain("'/manifest.json'");
            swJs.Should().Contain("'/app.js'");
            swJs.Should().Contain("https://cdn.tailwindcss.com");
            swJs.Should().Contain("https://unpkg.com/lucide@latest");
            swJs.Should().Contain("https://cdn.jsdelivr.net/npm/chart.js");

            // Install event handler
            swJs.Should().Contain("addEventListener('install'", "Service worker must handle 'install' event");
            swJs.Should().Contain("caches.open", "Service worker install handler must open the cache");

            // Fetch event handler (cache first fallback)
            swJs.Should().Contain("addEventListener('fetch'", "Service worker must handle 'fetch' event");
            swJs.Should().Contain("caches.match", "Service worker must check cache first");
            swJs.Should().Contain("respondWith", "Service worker must respondWith matching cache or fetch");
        }

        [Fact]
        public void AppJs_ShouldContainEssentialFrontendAndPwaLogic()
        {
            var wwwrootDir = GetWwwrootDirectory();
            var appJsPath = Path.Combine(wwwrootDir, "app.js");
            var appJs = File.ReadAllText(appJsPath);

            // Service worker registration
            appJs.Should().Contain("navigator.serviceWorker.register('/sw.js')", "app.js must register the service worker");

            // PWA install prompt handler
            appJs.Should().Contain("beforeinstallprompt", "app.js must listen for beforeinstallprompt event");
            appJs.Should().Contain("btnInstallPwa", "app.js should handle PWA install button");

            // Theme toggle / Dark mode logic
            appJs.Should().Contain("themeToggle", "app.js must reference themeToggle");
            appJs.Should().Contain("localStorage.setItem('theme'", "app.js must persist selected theme in localStorage");
            appJs.Should().Contain("add('dark')", "app.js must toggle dark class on root");

            // View switching logic
            appJs.Should().Contain("function switchView", "app.js must implement switchView");
            appJs.Should().Contain(".view-section", "switchView should manipulate .view-section classes");

            // Split transaction logic
            appJs.Should().Contain("addSplitItem", "app.js must provide addSplitItem function");
            appJs.Should().Contain("calculateTotal", "app.js must provide calculateTotal function");
            appJs.Should().Contain("split-amount", "app.js must sum amounts by split-amount class");

            // Statement reconciliation drag-drop logic
            appJs.Should().Contain("dropZone", "app.js must handle dropZone interactions");
            appJs.Should().Contain("dragover", "app.js must handle dragover event");
            appJs.Should().Contain("drop", "app.js must handle drop event");
        }

        [Fact]
        public void ProgramCs_ShouldConfigureStaticFilesAndSpaFallbackRouting()
        {
            var programCsPath = GetWebApiProgramCsPath();
            var programCs = File.ReadAllText(programCsPath);

            programCs.Should().Contain("app.UseDefaultFiles();", "Program.cs must call UseDefaultFiles");
            programCs.Should().Contain("app.UseStaticFiles();", "Program.cs must call UseStaticFiles");
            programCs.Should().Contain("app.MapFallbackToFile(\"index.html\");", "Program.cs must map fallback to index.html for SPA routing");
        }
    }
}
