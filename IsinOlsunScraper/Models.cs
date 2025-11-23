using System.Collections.Generic;

namespace IsinOlsunScraper
{
    public class CityConfig
    {
        public List<CityConfigItem> Cities { get; set; }
    }

    public class CityConfigItem
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public List<DistrictConfig> Districts { get; set; }
    }

    public class DistrictConfig
    {
        public string Name { get; set; }
        public string Url { get; set; }
    }

    public class IlanBilgisi
    {
        public string Baslik { get; set; }
        public string Firma { get; set; }
        public string IsTanimi { get; set; }
        public string CalismaTuru { get; set; }
        public string BasvuruSayisi { get; set; }
        public string YanHaklar { get; set; }
        public string IsverenHakkinda { get; set; }
        public string IlanDetayAciklama { get; set; }
        public string CalismaSaatleri { get; set; }
        public string CalismaGunleri { get; set; }
        public string UyelikTarihi { get; set; }
        public string IlanSayisi { get; set; }
        public string SonAktifTarih { get; set; }
        public string CompanyId { get; set; }
        public string OtherJobs { get; set; }
        public string Link { get; set; }
    }
}
