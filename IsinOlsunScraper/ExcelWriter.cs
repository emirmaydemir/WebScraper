using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IsinOlsunScraper
{
    public class ExcelWriter
    {
        private readonly string _filePath;
        public ExcelWriter(string filePath)
        {
            _filePath = filePath;
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            EnsureExcel();
        }

        private void EnsureExcel()
        {
            var fi = new FileInfo(_filePath);
            if (!fi.Exists)
            {
                using (var p = new ExcelPackage(fi))
                {
                    var ws = p.Workbook.Worksheets.Add("İlanlar");
                    var headers = new[]
                    {
                        "İş Başlığı","Firma Adı","İş Tanımı","Çalışma Türü","Başvuru Sayısı","Yan Haklar",
                        "İşveren Hakkında","İlan Detaylı Açıklama","Çalışma Saatleri","Çalışma Günleri",
                        "Üyelik Tarihi","Yayınladığı İlan Sayısı","Son Aktif Olduğu Tarih", "CompanyId", "İlanlar", "Link"
                    };
                    for (int i = 0; i < headers.Length; i++) ws.Cells[1, i + 1].Value = headers[i];
                    p.Save();
                }
            }
        }

        public void AppendIlanlar(List<IlanBilgisi> ilanlar)
        {
            var fi = new FileInfo(_filePath);
            int attempts = 0;
            while (IsFileLocked(fi) && attempts < 5)
            {
                attempts++;
                System.Threading.Thread.Sleep(500);
            }

            using (var p = new ExcelPackage(fi))
            {
                var ws = p.Workbook.Worksheets.FirstOrDefault() ?? p.Workbook.Worksheets.Add("İlanlar");
                int startRow = ws.Dimension?.End.Row + 1 ?? 2;

                foreach (var ilan in ilanlar)
                {
                    ws.Cells[startRow, 1].Value = ilan.Baslik;
                    ws.Cells[startRow, 2].Value = ilan.Firma;
                    ws.Cells[startRow, 3].Value = ilan.IsTanimi;
                    ws.Cells[startRow, 4].Value = ilan.CalismaTuru;
                    ws.Cells[startRow, 5].Value = ilan.BasvuruSayisi;
                    ws.Cells[startRow, 6].Value = ilan.YanHaklar;
                    ws.Cells[startRow, 7].Value = ilan.IsverenHakkinda;
                    ws.Cells[startRow, 8].Value = ilan.IlanDetayAciklama;
                    ws.Cells[startRow, 9].Value = ilan.CalismaSaatleri;
                    ws.Cells[startRow, 10].Value = ilan.CalismaGunleri;
                    ws.Cells[startRow, 11].Value = ilan.UyelikTarihi;
                    ws.Cells[startRow, 12].Value = ilan.IlanSayisi;
                    ws.Cells[startRow, 13].Value = ilan.SonAktifTarih;
                    ws.Cells[startRow, 14].Value = ilan.CompanyId;
                    ws.Cells[startRow, 15].Value = ilan.OtherJobs;
                    ws.Cells[startRow, 16].Value = ilan.Link;
                    startRow++;
                }

                ws.Cells[1, 1, startRow - 1, 14].AutoFitColumns();
                p.Save();
            }
        }

        private bool IsFileLocked(FileInfo file)
        {
            try
            {
                if (!file.Exists) return false;
                using (FileStream stream = file.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    stream.Close();
                }
            }
            catch (IOException) { return true; }
            return false;
        }
    }
}
