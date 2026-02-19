using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WebScraper
{
    /// <summary>
    /// SecretCV ilanlarý için Excel yazýcý sýnýfý
    /// </summary>
    public class SecretCVExcelWriter
    {
        private readonly string _filePath;

        public SecretCVExcelWriter(string filePath)
        {
            _filePath = filePath;
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            EnsureExcel();
        }

        /// <summary>
        /// Excel dosyasýný oluþturur ve baþlýklarý ekler
        /// </summary>
        private void EnsureExcel()
        {
            var fi = new FileInfo(_filePath);
            if (!fi.Exists)
            {
                using (var package = new ExcelPackage(fi))
                {
                    var worksheet = package.Workbook.Worksheets.Add("Ýlanlar");

                    // Baþlýk satýrý
                    var headers = new[]
                    {
                        "Ýþ Baþlýðý",
                        "Firma Adý",
                        "Lokasyon",
                        "Ýlçe",
                        "Ýlan Tarihi",
                        "Ýlan Linki",
                        "Kaynak Þehir",
                        "Ýþ Açýklamasý",
                        "Ýstenen Yetenek ve Uzmanlýklar"
                    };

                    // Baþlýklarý yaz ve formatla
                    for (int i = 0; i < headers.Length; i++)
                    {
                        var cell = worksheet.Cells[1, i + 1];
                        cell.Value = headers[i];
                        cell.Style.Font.Bold = true;
                        cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                        cell.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                    }

                    // Sütun geniþliklerini ayarla
                    worksheet.Column(1).Width = 50;  // Ýþ Baþlýðý
                    worksheet.Column(2).Width = 30;  // Firma Adý
                    worksheet.Column(3).Width = 20;  // Lokasyon
                    worksheet.Column(4).Width = 30;  // Ýlçe
                    worksheet.Column(5).Width = 15;  // Ýlan Tarihi
                    worksheet.Column(6).Width = 60;  // Ýlan Linki
                    worksheet.Column(7).Width = 20;  // Kaynak Þehir
                    worksheet.Column(8).Width = 80;  // Ýþ Açýklamasý
                    worksheet.Column(9).Width = 80;  // Ýstenen Yetenek ve Uzmanlýklar

                    // Text wrap aktif
                    worksheet.Cells[1, 8].Style.WrapText = true;
                    worksheet.Cells[1, 9].Style.WrapText = true;

                    package.Save();
                }
            }
        }

        /// <summary>
        /// Ýlan listesini Excel'e ekler
        /// </summary>
        public void AppendJobs(List<SecretCVJobListing> jobs)
        {
            if (jobs == null || jobs.Count == 0)
            {
                return;
            }

            var fi = new FileInfo(_filePath);
            int attempts = 0;
            const int maxAttempts = 10;

            // Dosya kilitli ise bekle
            while (IsFileLocked(fi) && attempts < maxAttempts)
            {
                attempts++;
                System.Threading.Thread.Sleep(500);
            }

            if (attempts >= maxAttempts)
            {
                throw new IOException($"Excel dosyasý kilitli ve açýlamadý: {_filePath}");
            }

            using (var package = new ExcelPackage(fi))
            {
                var worksheet = package.Workbook.Worksheets.FirstOrDefault();
                if (worksheet == null)
                {
                    worksheet = package.Workbook.Worksheets.Add("Ýlanlar");
                }

                // Son satýrý bul
                int startRow = worksheet.Dimension?.End.Row + 1 ?? 2;

                // Her ilaný yaz
                foreach (var job in jobs)
                {
                    int col = 1;
                    
                    // Ýþ Baþlýðý
                    worksheet.Cells[startRow, col++].Value = job.JobTitle ?? "";
                    
                    // Firma Adý
                    worksheet.Cells[startRow, col++].Value = job.CompanyName ?? "";
                    
                    // Lokasyon
                    worksheet.Cells[startRow, col++].Value = job.Location ?? "";
                    
                    // Ýlçe
                    worksheet.Cells[startRow, col++].Value = job.District ?? "";
                    
                    // Ýlan Tarihi
                    worksheet.Cells[startRow, col++].Value = job.PostDate ?? "";
                    
                    // URL için hyperlink ekle
                    if (!string.IsNullOrEmpty(job.JobUrl))
                    {
                        var urlCell = worksheet.Cells[startRow, col];
                        urlCell.Hyperlink = new Uri(job.JobUrl);
                        urlCell.Value = job.JobUrl;
                        urlCell.Style.Font.UnderLine = true;
                        urlCell.Style.Font.Color.SetColor(System.Drawing.Color.Blue);
                    }
                    col++;

                    // Kaynak Þehir
                    worksheet.Cells[startRow, col++].Value = job.SourceCity ?? "";

                    // Ýþ Açýklamasý (Text wrap aktif)
                    var descCell = worksheet.Cells[startRow, col++];
                    descCell.Value = job.JobDescription ?? "";
                    descCell.Style.WrapText = true;
                    descCell.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Top;

                    // Ýstenen Yetenek ve Uzmanlýklar (Text wrap aktif)
                    var skillsCell = worksheet.Cells[startRow, col++];
                    skillsCell.Value = job.RequiredSkills ?? "";
                    skillsCell.Style.WrapText = true;
                    skillsCell.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Top;

                    // Satýr yüksekliðini ayarla (detaylý açýklamalar için)
                    worksheet.Row(startRow).Height = 60;

                    startRow++;
                }

                package.Save();
            }
        }

        /// <summary>
        /// Dosyanýn kilitli olup olmadýðýný kontrol eder
        /// </summary>
        private bool IsFileLocked(FileInfo file)
        {
            try
            {
                if (!file.Exists)
                {
                    return false;
                }

                using (FileStream stream = file.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    stream.Close();
                }
            }
            catch (IOException)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Excel dosyasýný temizler (tüm verileri siler)
        /// </summary>
        public void Clear()
        {
            var fi = new FileInfo(_filePath);
            if (fi.Exists)
            {
                fi.Delete();
            }
            EnsureExcel();
        }
    }
}
