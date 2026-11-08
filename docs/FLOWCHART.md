# Görev Atama Akış Şeması

[README'ye dön](../README.md)

Bu şema `AssignmentService.AssignTask(taskId)` metodunu gösterir. Görev oluşturulduktan sonra veya kullanıcı tekrar atama istediğinde çağrılır. GitHub, aşağıdaki Mermaid bloğunu çizim olarak gösterir.

```mermaid
flowchart TD
    Start([Başla: görev ID]) --> Find[Görevi bul]
    Find --> Pending{Görev var ve Pending mi?}
    Pending -- Hayır --> Finish([Bitir])
    Pending -- Evet --> Load[Developer'ları görevleriyle getir<br/>Seçilen developer = yok]
    Load --> Next{İncelenecek developer var mı?}

    Next -- Evet --> Last[Developer'ın en son atanan görevini bul<br/>AssignedAt değeri en yeni olan kayıt]
    Last --> HasLast{Önceki atama var mı?}
    HasLast -- Hayır --> First
    HasLast -- Evet --> Adjacent{Yeni ve son zorluğun<br/>mutlak farkı 1 mi?}
    Adjacent -- Evet: uygun değil --> Next
    Adjacent -- Hayır: uygun --> First

    First{Seçilen developer var mı?}
    First -- Hayır --> Select[Bu developer'ı seç]
    First -- Evet --> Active{Adayın aktif yükü<br/>daha düşük mü?}
    Active -- Evet --> Select
    Active -- Hayır --> ActiveEqual{Aktif yükler eşit mi?}
    ActiveEqual -- Hayır --> Next
    ActiveEqual -- Evet --> Completed{Adayın tamamlanan yükü<br/>daha düşük mü?}
    Completed -- Evet --> Select
    Completed -- Hayır --> CompletedEqual{Tamamlanan yükler eşit mi?}
    CompletedEqual -- Hayır --> Next
    CompletedEqual -- Evet --> Id{Adayın ID'si<br/>daha düşük mü?}
    Id -- Evet --> Select
    Id -- Hayır --> Next
    Select --> Next

    Next -- Hayır --> Found{Developer seçildi mi?}
    Found -- Hayır --> Wait[Görevi Pending bırak]
    Wait --> Finish
    Found -- Evet --> Assign[DeveloperId bilgisini yaz<br/>Durumu Assigned yap<br/>AssignedAt = UTC zamanı]
    Assign --> Save[SaveChanges ile kaydet]
    Save --> Finish
```

**Hesaplar:** Aktif yük, `Assigned` ve `InProgress`; tamamlanan yük, `Completed` görevlerin zorluk toplamıdır. Karşılaştırmalar yalnızca uygun adaylar arasında yapılır.

**Ardışıklık örneği:** Son atanan zorluk 4 ise 3 ve 5 reddedilir; 4 kabul edilir. Son görev tamamlanmış olsa da değerlendirilir. Önceki ataması olmayan developer uygundur.

**Sınır:** Son atama mevcut görev kayıtlarından bulunur. Silinen veya Pending'e döndürülerek ataması temizlenen görevler bu geçmişte korunmaz. Uygun aday bulunamadığında otomatik bir bekleme döngüsü çalışmaz; kullanıcı daha sonra tekrar atama isteyebilir.
