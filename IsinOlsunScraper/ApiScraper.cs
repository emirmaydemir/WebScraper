using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading;

namespace ElemanNetScraper
{
    public class IlanUnvaniModel
    {
        public string value { get; set; }
    }

    public class ApiScraper
    {
        private const string AutoCompleteUrlBase = "https://www.eleman.net/arama-complete.php?action=pozisyon&term=";
        private readonly HttpClient _httpClient;

        private readonly char[] _searchChars =
            "ABCÇDEFGHIİJKLMNOÖPRŞSTUÜVYZ0123456789".ToCharArray();

        private int _completedRequests = 0;
        private int _totalRequestsStep2 = 0;

        public ApiScraper()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("C# Scraper/1.0");
        }

        public async Task<List<string>> GetAllJobTitlesAsync()
        {
            Console.WriteLine("\n--- Otomatik Tamamlama API'sinden İŞ POZİSYONLARI Çekiliyor (action=pozisyon) ---");
            Console.WriteLine("⚠️ DİKKAT: 1 ve 2 harfli kombinasyonlar deneniyor. Bu işlem 1 saat kadar sürebilir.");

            var jobTitles = new HashSet<string>();

            Console.WriteLine("\n--- ADIM 1: Tek Harfli Kombinasyonlar Başlatılıyor (~35 istek) ---");
            var tasks1 = new List<Task>();
            foreach (var char1 in _searchChars)
            {
                tasks1.Add(FetchJobTitlesByTermAsync(char1.ToString(), jobTitles));
            }
            await Task.WhenAll(tasks1);
            Console.WriteLine($"--- ADIM 1 Tamamlandı. Toplam benzersiz unvan: {jobTitles.Count} ---");


            Console.WriteLine("\n--- ADIM 2: İki Harfli Kombinasyonlar Başlatılıyor (Toplam ~1225 istek) ---");

            _completedRequests = 0;
            _totalRequestsStep2 = _searchChars.Length * _searchChars.Length;

            var tasks2 = new List<Task>();
            foreach (var char1 in _searchChars)
            {
                foreach (var char2 in _searchChars)
                {
                    string term = $"{char1}{char2}";
                    tasks2.Add(FetchJobTitlesByTermWithProgressAsync(term, jobTitles));
                }
            }

            var progressTask = Task.Run(() => ShowProgress());

            await Task.WhenAll(tasks2);
            await progressTask;

            Console.WriteLine("\n" + new string('-', 40));
            Console.WriteLine($"--- ADIM 2 Tamamlandı. Toplam benzersiz unvan: {jobTitles.Count} ---");

            var finalTitles = jobTitles.OrderBy(t => t).ToList();
            Console.WriteLine($"\n✅ API'den toplam {finalTitles.Count} benzersiz iş adı çekildi.");

            WriteToCsv(finalTitles, "is_pozisyonlari.csv");

            return finalTitles;
        }

        private async Task FetchJobTitlesByTermWithProgressAsync(string term, HashSet<string> jobTitles)
        {
            await FetchJobTitlesByTermAsync(term, jobTitles);
            Interlocked.Increment(ref _completedRequests);
        }

        private void ShowProgress()
        {
            while (_completedRequests < _totalRequestsStep2)
            {
                int percentage = (_completedRequests * 100) / _totalRequestsStep2;

                Console.Write($"\r⚙️ ADIM 2 İlerleme: {_completedRequests}/{_totalRequestsStep2} istek tamamlandı ({percentage}%)");

                Thread.Sleep(500);
            }
        }


        private async Task FetchJobTitlesByTermAsync(string term, HashSet<string> jobTitles)
        {
            await Task.Delay(100);

            try
            {
                string url = $"{AutoCompleteUrlBase}{term}";

                if (term.Length == 1)
                {
                    Console.WriteLine($"-> '{term}' terimi için istek gönderiliyor...");
                }

                string jsonResponse = await _httpClient.GetStringAsync(url);

                var models = JsonSerializer.Deserialize<List<IlanUnvaniModel>>(jsonResponse);

                if (models != null && models.Any())
                {
                    int addedCount = 0;

                    lock (jobTitles)
                    {
                        foreach (var model in models)
                        {
                            if (!string.IsNullOrEmpty(model.value))
                            {
                                if (jobTitles.Add(model.value.Trim()))
                                {
                                    addedCount++;
                                }
                            }
                        }
                    }

                    if (addedCount > 0)
                    {
                        Console.WriteLine($"\n   '{term}' teriminden {models.Count} sonuç alındı. YENİ EKLENEN: {addedCount}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n ! HATA: '{term}' terimi çekilirken sorun oluştu: {ex.Message}");
            }
        }

        private void WriteToCsv(List<string> titles, string filePath)
        {
            try
            {
                File.WriteAllLines(filePath, titles, Encoding.UTF8);

                Console.WriteLine($"\n✅ Tüm pozisyonlar '{filePath}' dosyasına başarıyla, UTF-8 (Türkçe karakter destekli) formatında yazıldı.");
                Console.WriteLine($"Toplam satır sayısı: {titles.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($" ! HATA: CSV dosyasına yazılırken sorun oluştu: {ex.Message}");
            }
        }
    }
}