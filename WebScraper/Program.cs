using System;
using System.IO;
using Newtonsoft.Json;
using System.Linq;

namespace WebScraper
{
    class Program
    {
        static int Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔═══════════════════════════════════════╗");
            Console.WriteLine("║   SecretCV Şehir Bazlı İlan Scraper  ║");
            Console.WriteLine("╚═══════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();

            try
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("🌍 SecretCV sitesinden şehir listesi otomatik çekiliyor...");
                Console.ResetColor();
                Console.WriteLine();
                
                CityConfigRoot config = null;
                
                try
                {
                    using (var cityScraper = new CityScraper())
                    {
                        var cities = cityScraper.GetAllCities();
                        
                        if (cities == null || cities.Count == 0)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("❌ HATA: Siteden şehir listesi çekilemedi!");
                            Console.ResetColor();
                            Console.WriteLine("\nÇıkmak için bir tuşa basın...");
                            Console.ReadKey();
                            return 1;
                        }
                        
                        config = new CityConfigRoot { Cities = cities };
                        
                        // Çekilen listeyi JSON olarak kaydet (yedek için)
                        var cfgPath = Path.Combine(AppContext.BaseDirectory, "cityconfig.json");
                        var configJson = JsonConvert.SerializeObject(config, Formatting.Indented);
                        File.WriteAllText(cfgPath, configJson);
                        
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"✓ {cities.Count} şehir siteden başarıyla çekildi!");
                        Console.WriteLine($"✓ Şehir listesi cityconfig.json'a yedeklendi.");
                        Console.ResetColor();
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"❌ Siteden şehir çekerken hata: {ex.Message}");
                    Console.ResetColor();
                    Console.WriteLine("\nÇıkmak için bir tuşa basın...");
                    Console.ReadKey();
                    return 1;
                }
                
                Console.WriteLine();

                // Klasörleri oluştur
                Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "logs"));
                Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "output"));

                // SecretCV Runner'ı başlat
                var runner = new SecretCVRunner();

                try
                {
                    runner.RunAllCitiesAsync(config).GetAwaiter().GetResult();
                }
                catch (AggregateException agEx)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\n❌ TOPLU HATA:");
                    Console.ResetColor();

                    foreach (var inner in agEx.Flatten().InnerExceptions)
                    {
                        Console.WriteLine($"  • {inner.Message}");
                        File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "logs", "fatal.log"),
                            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {inner}\n\n");
                    }
                }

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ Tüm işlemler tamamlandı!");
                Console.ResetColor();
                Console.WriteLine("\nExcel dosyaları: " + Path.Combine(AppContext.BaseDirectory, "output"));
                Console.WriteLine("Log dosyaları: " + Path.Combine(AppContext.BaseDirectory, "logs"));
                Console.WriteLine("\nÇıkmak için bir tuşa basın...");
                Console.ReadKey();
                return 0;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n❌ KRİTİK HATA:");
                Console.WriteLine(ex.Message);
                Console.ResetColor();

                File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "logs", "fatal.log"),
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - CRITICAL ERROR:\n{ex}\n\n");

                Console.WriteLine("\nÇıkmak için bir tuşa basın...");
                Console.ReadKey();
                return 2;
            }
        }
    }
}