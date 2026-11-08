# Netlog Task Distribution Case Study

Sekiz developer arasında görevleri zorluk seviyesine ve mevcut iş yüküne göre otomatik dağıtan bir web uygulamasıdır. Netlog teknik değerlendirmesi için hazırlanmıştır.

## Teknolojiler

C#, ASP.NET Core MVC (.NET 10), Razor, Entity Framework Core (Code First), SQLite ve Bootstrap.

## Kurulum ve çalıştırma

Bilgisayarda **.NET 10 SDK** ve Git kurulu olmalıdır. İlk çalıştırmada NuGet paketlerini indirmek için internet bağlantısı gerekir.

```bash
git clone https://github.com/AFSERO/Netlog.TaskManagement.git
cd Netlog.TaskManagement/Netlog.TaskManagement.Web
dotnet restore
dotnet run --launch-profile http
```

Tarayıcıdan [http://localhost:5239](http://localhost:5239) adresini açın. Alternatif olarak Visual Studio'da solution dosyasını açıp Web projesini çalıştırabilirsiniz.

Uygulama açılırken EF Core migration'ları otomatik uygulanır. `netlog.db` ve tabloları oluşturulur; developer tablosu boşsa sekiz developer eklenir. **Manuel migration komutu veya ayrı bir veritabanı sunucusu gerekmez.** Sonraki açılışlarda mevcut veriler korunur ve varsa yeni migration'lar uygulanır.

SQLite bağlantısı `appsettings.json` içindedir. Yukarıdaki çalıştırma adımlarında veritabanı Web proje klasöründe oluşur. Yerel veritabanı ve yardımcı dosyaları Git'e dahil edilmez.

## Kullanım

- Görev adı ve **1–8 arasında zorluk** girerek görev oluşturun. Kayıt sonrasında otomatik atama denenir.
- Görevler ID'ye göre azalan sırada listelenir; yeni görev en üstte görünür.
- Developer tablosunda aktif görev sayısı, aktif iş yükü ve tamamlanan iş yükü gösterilir.
- Görev adı düzenlenebilir. Zorluk yalnızca `Pending` durumunda değiştirilebilir.
- Durum listeden güncellenebilir. `Completed` durumundaki görev doğrudan silinemez.
- Atanamayan görev için **Atamayı Tekrar Dene** kullanılabilir. Bekleyen görevler arka planda otomatik olarak yeniden dağıtılmaz.

| Durum | Anlamı |
| --- | --- |
| `Pending` | Atama bekliyor. |
| `Assigned` | Developer'a atandı. |
| `InProgress` | Üzerinde çalışılıyor. |
| `Completed` | Tamamlandı. |

## Görev dağıtım kuralları

Bütün developer'lar her zorluk seviyesindeki görevi yapabilir. Sistem önce uygunluğu kontrol eder, ardından uygun adaylar arasında şu öncelikleri uygular:

1. **En düşük aktif iş yükü:** `Assigned` ve `InProgress` görevlerin zorluk toplamı.
2. Eşitlikte **en düşük tamamlanan iş yükü:** `Completed` görevlerin zorluk toplamı.
3. Eşitlik sürüyorsa **en düşük Developer ID**.

İş yükü görev sayısı değildir: zorlukları 2 ve 5 olan iki aktif görevin yükü **7**'dir.

**Uygunluk:** Developer'ın `AssignedAt` değeri en yeni olan görevi bulunur; bu görev tamamlanmış olsa da karşılaştırmaya dahildir. Yeni görevle zorluk farkının mutlak değeri **1 ise atama yapılmaz**. Örneğin son zorluk 4 ise 3 ve 5 uygun değildir; aynı zorluk 4 veya farkı daha büyük zorluklar uygundur. Daha önce görev atanmamış developer uygundur.

Uygun aday yoksa görev `Pending` kalır. Aday seçilirse `DeveloperId`, `Assigned` durumu ve UTC atama zamanı kaydedilir. Pending olmayan bir görev atama servisi tarafından yeniden atanmaz.

**[Algoritmanın ayrıntılı akış şeması →](docs/FLOWCHART.md)**

## Kodun yapısı

Uygulama tek bir MVC projesinde, sorumluluklarına göre klasörlere ayrılmıştır:

| Bölüm | Sorumluluk |
| --- | --- |
| `Controllers/TaskController.cs` | `TasksController`: listeleme, CRUD, durum güncelleme ve tekrar atama istekleri. |
| `Services/AssigmentService.cs` | `AssignmentService`: uygunluk, iş yükü hesabı ve developer seçimi. |
| `Models` | `TaskItem`, `Developer`, görev durumları ve hata ekranı modeli. |
| `Data/AppDbContext.cs` | EF Core üzerinden görev ve developer verilerine erişim. |
| `Migrations` | Veritabanı şemasını oluşturan Code First migration'ları. |
| `Views` | Razor ekranları; iş yükü özeti ve görev işlemleri. |
| `Program.cs` | Servis kayıtları, otomatik veritabanı hazırlığı ve yönlendirme. |

Yeni görev önce kaydedilir; oluşan ID ile atama servisi çağrılır. Servis developer'ları görevleriyle birlikte okur, seçimi yapar ve atama sonucunu kaydeder. Controller ve servis, dependency injection ile sağlanan `AppDbContext` üzerinden çalışır.

## Mevcut kapsam ve sınırlar

- Kullanıcı girişi ve rol bazlı yetkilendirme yoktur; zorluk bilgisi formdan girilir. Sekiz farklı zorluk düzeyi 1–8 sayılarıyla temsil edilir; ayrı bir işlem türü kataloğu bulunmaz.
- Durumlar arasında katı bir geçiş sırası uygulanmaz. Görev `Pending` yapılırsa atama ve atama zamanı temizlenir. Tamamlanan bir görev de yeniden açılabilir; silme kontrolü yalnızca mevcut duruma bakar.
- Son atama hesabı ayrı bir geçmiş tablosundan değil, mevcut görev kayıtlarından yapılır. Silme veya Pending'e dönüş bu hesabı etkileyebilir.
- Otomatik test projesi ve eşzamanlı atamalar için ek bir koruma mekanizması bulunmaz. Oluşturma ve atama iki ayrı kayıt adımıdır.
