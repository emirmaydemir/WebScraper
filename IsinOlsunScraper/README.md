# ?? SecretCV Ýlan Scraper - Profesyonel Þehir Bazlý Tarama Sistemi

## ?? Özellikler

? **Tam Otomatik Tarama**: 40+ þehir için otomatik ilan tarama  
? **Detaylý Bilgi Çekme**: Her ilan için detay sayfasýndan tam bilgi  
? **Akýllý Sayfalama**: 50'þer ilan otomatik sayfa geçiþi  
? **Excel Çýktýsý**: Her þehir için ayrý, düzenli Excel dosyasý  
? **Hata Toleransý**: Bir þehir hata verse bile diðerleri devam eder  
? **Rate Limiting**: Site yükünü düþürmek için akýllý bekleme  
? **Detaylý Loglama**: Her þehir için ayrý log dosyasý  

## ?? Çekilen Bilgiler

### Liste Sayfasýndan:
- Ýþ Baþlýðý
- Firma Adý
- Lokasyon
- Ýlçe
- Ýlan Tarihi
- Ýlan URL'si

### Detay Sayfasýndan:
- **Ýþ Açýklamasý** (Tam metin)
- **Ýstenen Yetenek ve Uzmanlýklar** (Tam liste)

## ?? Kurulum ve Çalýþtýrma

### 1. Ön Gereksinimler
- .NET Framework 4.7.2
- Google Chrome (headless mode için)
- ChromeDriver (otomatik indirilir)

### 2. Yapýlandýrma

`cityconfig.json` dosyasýný eski dosyanýn yerine koyun (veya adýný deðiþtirin):

```bash
# Eski dosyayý yedekle
ren cityconfig.json cityconfig.old.json

# Yeni dosyayý kullan
ren cityconfig_new.json cityconfig.json
```

### 3. Çalýþtýrma

```bash
# Build
dotnet build

# Run
dotnet run
```

veya Visual Studio'da **F5** tuþuna basýn.

## ?? Çýktý Yapýsý

```
output/
  ??? Istanbul_Anadolu.xlsx
  ??? Istanbul_Avrupa.xlsx
  ??? Ankara.xlsx
  ??? Izmir.xlsx
  ??? ... (40+ þehir)

logs/
  ??? Istanbul_Anadolu.log
  ??? Istanbul_Avrupa.log
  ??? Ankara.log
  ??? ... (40+ þehir)
```

## ?? Excel Formatý

| Sütun | Açýklama | Geniþlik |
|-------|----------|----------|
| Ýþ Baþlýðý | Ýlanýn baþlýðý | 50 |
| Firma Adý | Þirket adý | 30 |
| Lokasyon | Þehir bilgisi | 20 |
| Ýlçe | Ýlçe listesi | 30 |
| Ýlan Tarihi | Yayýn tarihi | 15 |
| Ýlan Linki | Týklanabilir URL | 60 |
| Kaynak Þehir | Hangi þehir aramasýndan geldiði | 20 |
| **Ýþ Açýklamasý** | Detaylý iþ tanýmý | 80 |
| **Ýstenen Yetenek ve Uzmanlýklar** | Aranan nitelikler | 80 |

## ?? Teknik Detaylar

### Mimari
```
SecretCVRunner
  ??> SecretCVScraper (her þehir için)
       ??> Liste sayfasýný tara (sayfa sayfa)
       ??> Her ilan için detay sayfasýný aç
       ??> SecretCVExcelWriter'a kaydet
```

### Performans
- **Ýlan baþýna**: ~3-4 saniye (liste + detay)
- **Sayfa baþýna**: ~50 ilan
- **Örnek süre**: 
  - 872 ilaný olan Ankara = 18 sayfa × 4sn/ilan × 50 = ~60 dakika
  - Toplam 40 þehir = Yaklaþýk 8-12 saat

### Rate Limiting
```csharp
Sayfa yükleme: 3000ms
Detay sayfasý: 2000ms
Sayfalar arasý: 2000ms
Þehirler arasý: 2000ms
```

## ??? Güvenlik ve Kararlýlýk

1. **Bot Tespiti Engelleme**
   - User-Agent ayarlanmýþ
   - Gerçekçi bekleme süreleri
   - Headless Chrome kullanýmý

2. **Hata Yönetimi**
   - Her þehir baðýmsýz try-catch
   - Her sayfa için hata toleransý
   - Detaylý hata loglamasý

3. **Memory Yönetimi**
   - Her þehir sonrasý GC.Collect
   - Selenium driver'larý düzgün kapatýlýr
   - Excel dosyalarý güvenli yazýlýr

## ?? Önemli Notlar

?? **DÝKKAT**: 
- Ýlk çalýþtýrmada ChromeDriver otomatik indirilir
- Ýnternet baðlantýnýz stabil olmalý
- 40 þehir taramasý uzun sürer (~8-12 saat)
- **Test için önce 2-3 þehirle baþlamanýz önerilir**

?? **ÝPUÇLARI**:
- Gece çalýþtýrarak sabah sonuçlarý alabilirsiniz
- Önce az þehirle test edin
- Log dosyalarýný düzenli kontrol edin
- Hata durumunda log dosyalarýna bakýn

## ?? Sorun Giderme

### ChromeDriver bulunamadý
```bash
# Proje klasöründe:
dotnet restore
```

### Excel dosyasý kilitli
- Excel dosyalarýný kapatýn
- Programý yeniden baþlatýn

### Detay bilgileri çekilmiyor
- Ýnternet baðlantýnýzý kontrol edin
- Log dosyalarýna bakýn
- Headless mode'u kapatýp test edin (SecretCVScraper.cs'de)

## ?? Destek

Herhangi bir sorun yaþarsanýz:
1. `logs/` klasöründeki hata loglarýný kontrol edin
2. `fatal.log` dosyasýna bakýn
3. GitHub issues açýn

## ?? Baþarýlar!

Artýk tüm SecretCV ilanlarýný otomatik toplayabilirsiniz!

---

**Son Güncelleme**: 2025-01-11  
**Versiyon**: 2.0.0 - SecretCV Özel
