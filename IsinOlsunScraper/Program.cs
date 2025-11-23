using System;
using System.IO;
using Newtonsoft.Json;

namespace IsinOlsunScraper
{
    class Program
    {
        static int Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            try
            {
                var cfgPath = Path.Combine(AppContext.BaseDirectory, "cityconfig.json");
                if (!File.Exists(cfgPath))
                {
                    Console.WriteLine($"cityconfig.json bulunamadı. Lütfen {cfgPath} dosyasını oluştur ve URL'leri ekle.");
                    return 1;
                }

                var json = File.ReadAllText(cfgPath);

                var config = JsonConvert.DeserializeObject<CityConfig>(json);

                if (config == null || config.Cities == null || config.Cities.Count == 0)
                {
                    Console.WriteLine("cityconfig.json içinde şehir bulunamadı.");
                    return 1;
                }

                Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "logs"));
                Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "output"));

                var runner = new Runner();

                try
                {
                    runner.RunAllCitiesAsync(config).GetAwaiter().GetResult();
                }
                catch (AggregateException agEx)
                {
                    foreach (var inner in agEx.Flatten().InnerExceptions)
                    {
                        File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "logs", "fatal.log"),
                            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {inner}\n\n");
                    }
                }

                Console.WriteLine("\nTüm işler tamamlandı. Çıkmak için bir tuşa basın...");
                Console.ReadKey();
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("KRİTİK HATA: " + ex);
                File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "logs", "fatal.log"),
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {ex}\n\n");
                return 2;
            }
        }
    }
}
