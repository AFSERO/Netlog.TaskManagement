# Mimari ve Uygulama Akışı

[README](../README.md) · [Görev atama flowchart'ı](FLOWCHART.md)

Bu belge, mevcut kodun nasıl çalıştığını ve tasarım tercihlerinin nedenlerini açıklar. Kod blokları kaynak dosyalardan alınmış kısa parçalardır.

## 1. Sorumluluklar ve bağımlılıklar

Proje tek bir ASP.NET Core MVC uygulamasıdır. Klasörler sorumluluklarına göre ayrılmıştır; ayrı uygulama/veri katmanı projeleri bulunmaz.

```text
Tarayıcı isteği → TasksController → Razor View → HTML yanıtı
                      │
                      ├─ CRUD ve listeleme → AppDbContext → SQLite
                      └─ Görev atama → AssignmentService → AppDbContext → SQLite
```

- **Controller:** Form verisini alır, doğrulama ve CRUD işlemlerini yürütür, uygun ekranı veya yönlendirmeyi döndürür.
- **AssignmentService:** Atama uygunluğunu, iş yüklerini ve developer seçimini hesaplar; atama sonucunu kaydeder.
- **Modeller:** Görev ve developer bilgilerini temsil eder.
- **AppDbContext:** EF Core üzerinden veri okuma ve kaydetmeyi sağlar.
- **Razor ekranları:** Formları ve tabloları oluşturur. Developer yüklerinin ekrandaki özeti halen `Index.cshtml` içinde hesaplanır; atama hesabı servistedir.

Algoritmayı ayrı serviste tutmak controller'ın seçim ayrıntılarını bilmesini azaltır. Controller ve servis somut `AppDbContext` sınıfına bağımlıdır; veri erişimi için ayrıca repository veya interface eklenmemiştir.

## 2. Başlangıç ve dependency injection

[Program.cs](../Netlog.TaskManagement.Web/Program.cs) içinde veri erişimi ve atama servisi kaydedilir:

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<AssignmentService>();
```

`UseSqlite`, `appsettings.json` içindeki `DefaultConnection` bağlantısını kullanır. Bağlantı `Data Source=netlog.db` olarak tanımlıdır.

`AddDbContext` ve `AddScoped` ile bu nesneler bir HTTP isteği boyunca kullanılır. Controller ve servis aynı istek kapsamında aynı context'i alır. Controller nesneleri kendisi oluşturmaz; constructor parametreleri ASP.NET Core tarafından sağlanır:

```csharp
public TasksController(
    AppDbContext context,
    AssignmentService assignmentService)
{
    _context = context;
    _assignmentService = assignmentService;
}
```

Başlangıçta ayrıca bir servis kapsamı açılır ve `db.Database.Migrate()` çağrılır. Veritabanı yoksa oluşturulur, bekleyen migration'lar uygulanır. Developer tablosu tamamen boşsa sekiz developer eklenir. Kullanıcıdan manuel migration çalıştırması beklenmez.

**Code First:** Önce C# modelleri tanımlanmış, şemayı oluşturan migration üretilmiştir. `Migrate()` mevcut migration'ları uygular; model değişikliklerinden kendiliğinden yeni migration üretmez.

## 3. Veri modeli ve EF Core

| Model / alan | Kullanımı |
| --- | --- |
| `TaskItem` | Görev başlığı, zorluk, durum ve zaman bilgileri. |
| `Developer` | Developer kimliği, adı ve görev listesi. |
| `TaskItemStatus` | `Pending`, `Assigned`, `InProgress`, `Completed` durumları. |
| `TaskItem.DeveloperId` | Görevin atandığı developer. Atama beklerken boş olabilir. |
| `TaskItem.AssignedAt` | Son atama karşılaştırmasında kullanılan UTC zaman; atama öncesinde boştur. |
| `Developer.Tasks` | Developer'a bağlı görevler; bir developer'ın birden fazla görevi olabilir. |

[AppDbContext.cs](../Netlog.TaskManagement.Web/Data/AppDbContext.cs), `Developers` ve `Tasks` için iki `DbSet` sunar. EF Core ilişkisini `Developer.Tasks` ve `TaskItem.DeveloperId` üzerinden kurar. Migration'da foreign key ve developer ID indeksi bulunur.

Veriler SQLite dosyasında saklanır. EF Core, sorgularla getirilen entity'lerin değişikliklerini izler. `Add` ve `Remove` yapılacak işlemi context'e bildirir; `SaveChanges()` bunları veritabanına yazar.

## 4. Formdan görev atamasına

[TasksController](../Netlog.TaskManagement.Web/Controllers/TaskController.cs) içindeki GET `Create`, boş görev formunu açar. Form gönderildiğinde POST `Create` çalışır.

Controller boş başlığı ve 1–8 dışında kalan zorluğu reddeder. Geçerli girdilerle varsayılan durumu `Pending` olan bir `TaskItem` oluşturur. Ardından:

```csharp
_context.Tasks.Add(taskItem);
_context.SaveChanges();

_assignmentService.AssignTask(taskItem.Id);
```

İlk `SaveChanges`, yeni kaydı yazar ve ID'nin oluşmasını sağlar. Atama servisi bu ID ile görevi bulur. Oluşturma ve atama ayrı kayıt adımlarıdır; ikisini birlikte kapsayan bir transaction tanımlanmamıştır.

Servis döndükten sonra `RedirectToAction("Index")` ile yeni bir listeleme isteğine yönlendirilir. Index görevleri ID'ye göre azalan sırayla alır. Görevler View'in modeli, developer'lar `ViewBag.Developers` üzerinden ekrana gönderilir. Razor bu bilgilerle HTML tablolarını üretir.

POST işlemlerinde `[ValidateAntiForgeryToken]` bulunur. Razor form tag helper'larıyla üretilen token'ın kontrol edilmesi, CSRF saldırılarına karşı koruma sağlar; kullanıcı kimlik doğrulaması veya rol yetkilendirmesi sağlamaz.

## 5. Developer seçimi

[AssignmentService](../Netlog.TaskManagement.Web/Services/AssigmentService.cs) önce görevi bulur. Görev yoksa veya Pending değilse hiçbir atama yapmadan döner:

```csharp
if (taskItem == null || taskItem.Status != TaskItemStatus.Pending)
{
    return;
}
```

Developer'lar görevleriyle birlikte okunur:

```csharp
var developers = _context.Developers
    .Include(d => d.Tasks)
    .ToList();
```

`Include`, yük ve son atama hesabında kullanılacak görevleri de getirir. Seçim, bu veriler üzerinde `foreach` ve koşullarla yapılır.

**Uygunluk:** `CanAssignTask`, atama zamanı en yeni olan görevi bulur. Hiç önceki atama yoksa developer uygundur. Aksi halde:

```csharp
int difference = Math.Abs(lastTask.Difficulty - difficulty);

return difference != 1;
```

Son zorluk 4 ise yeni 3 ve 5 reddedilir; 4 kabul edilir. Son görev tamamlanmış olsa da değerlendirilir. Görevin oluşturulma zamanı veya en büyük görev ID'si bu kontrolün ölçütü değildir.

**Yük karşılaştırması:** Uygun developer'lar arasında önce `Assigned` ve `InProgress` görevlerin zorluk toplamı karşılaştırılır. Eşitlikte `Completed` görevlerin zorluk toplamı, sonra küçük developer ID'si belirleyicidir. Bu öncelik sırası README ve flowchart'ta da gösterilmiştir.

Döngüde o ana kadar bulunan en uygun developer saklanır. Daha iyi bir aday bulunursa değiştirilir; kesin atama bütün developer'lar değerlendirildikten sonra yapılır. Uygun kişi yoksa görev Pending kalır. Kişi seçilmişse:

```csharp
taskItem.DeveloperId = selectedDeveloper.Id;
taskItem.Status = TaskItemStatus.Assigned;
taskItem.AssignedAt = DateTime.UtcNow;

_context.SaveChanges();
```

Bu alan değişiklikleri EF Core tarafından izlenir; `SaveChanges()` ile DB kaydı güncellenir.

## 6. Diğer işlemler ve tasarım sınırları

| İşlem | Mevcut davranış |
| --- | --- |
| Düzenleme | Başlık değişir. Zorluk yalnızca Pending ise 1–8 kontrolüyle değişir; düzenleme sonrasında otomatik atama denenmez. |
| Silme | Kayıt yoksa 404 döner. Mevcut durumu Completed olan görev doğrudan silinemez. |
| Durum güncelleme | Pending seçilirse developer ve atama zamanı temizlenir. Developer yoksa atama denenir; başarısızsa durum güncellenmez. |
| Tekrar atama | Yalnızca Pending görevler için atama servisi çağrılır. Otomatik arka plan denemesi yoktur. |

Okunabilirliği korumak için uygunluk, aktif yük ve tamamlanan yük ayrı özel metotlarda tutulmuştur. Küçük veri kümesinde basit döngüler kullanılmış; ayrı repository, genel amaçlı kural altyapısı veya çok projeli katmanlar eklenmemiştir.

Mevcut sınırlar:

- Son atama geçmişi mevcut görevlerden hesaplanır. Silme ve Pending'e dönüş bu geçmişi etkileyebilir; ayrı atama geçmişi tablosu yoktur.
- Durumlar arasında katı bir geçiş sırası yoktur. Completed görev yeniden açılıp ardından silinebilir. `UpdateStatus` içinde ModelState kontrolü bulunmadığından eksik/geçersiz durum girdilerinin ele alınması geliştirmeye açıktır.
- Controller ve servis EF Core'a doğrudan bağımlıdır. Ekrandaki yük hesabı servisteki hesapla aynı kuralları ayrıca uygular.
- Otomatik test projesi ve eşzamanlı seçimleri birlikte koruyan bir mekanizma yoktur. Bu konular ileride test ve işlem sınırı çalışması gerektirir.
