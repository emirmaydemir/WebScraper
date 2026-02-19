using System.Collections.Generic;

namespace IsinOlsunScraper
{
    // --- City Configuration Models ---
    public class CityConfigRoot
    {
        public List<City> Cities { get; set; }
    }

    public class City
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public List<District> Districts { get; set; }
    }

    public class District
    {
        public string Name { get; set; }
        public string Url { get; set; }
    }

    // --- SecretCV Job Listing Model ---
    public class SecretCVJobListing
    {
        // Basic Information from job card (List Page)
        public string JobTitle { get; set; }
        public string CompanyName { get; set; }
        public string Location { get; set; }
        public string District { get; set; }
        public string PostDate { get; set; }
        public string JobUrl { get; set; }
        public string SourceCity { get; set; }

        // Detail Page Information
        public string JobDescription { get; set; }  // İş Açıklaması
        public string RequiredSkills { get; set; }  // İstenen Yetenek ve Uzmanlıklar
    }

}