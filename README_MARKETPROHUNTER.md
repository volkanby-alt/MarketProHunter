# MarketProHunter

MarketProHunter, Amazon.com üzerinde ürün adaylarını taramak ve eBay satışına uygun olabilecek ürünleri ön filtrelemek için geliştirilen .NET 8 tabanlı Windows uygulamasıdır.

## Modül 1: Amazon Search Engine v1

İlk sürümde eklenenler:

- Amazon.com arama sayfası indirme
- ZIP hedefi: 07073
- Fiyat aralığı: 9-98 USD
- Amazon's Choice kontrolü
- Düşük stok uyarısı kontrolü
- "Customer usually keep this item" kontrolü
- VeRO / riskli marka filtresi
- CSV çıktı

## Çalıştırma

Windows 10 üzerinde .NET 8 SDK yüklüyse:

```bash
dotnet run --project src/MarketProHunter/MarketProHunter.csproj
```

Program arama kelimesi ister. Örnek:

```text
home cleaner
```

Çıktı dosyası `output` klasörüne CSV olarak yazılır.

## Not

Amazon zaman zaman bot koruması, captcha veya bölge/ZIP farklılığı gösterebilir. Bu ilk sürüm güvenli ve basit HTTP istekleriyle çalışır; giriş, captcha aşma veya gizli bypass içermez. Sonraki sürümde Windows arayüzü ve daha sağlam manuel kontrol ekranı eklenecektir.
