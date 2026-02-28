Bunu iş akışına şöyle oturturuz: **Müşteri şikayeti açıldığında** otomatik olarak **Halkla İlişkiler (PR/Customer Relations)** biriminin dashboard’una bildirim düşer; PR şikayet kaydının içinden müşteriyle “irtibat” sürecini yürütür.

## Yol haritası (şikayet → PR irtibat)

1. **Complaint (Şikayet) modülü**

* Müşteri (veya admin adına) bir şikayet kaydı açar.
* Alanlar: `Subject`, `Message`, `OrderId?`, `Status (New/InProgress/Resolved)`, `Priority`, `CreatedAt`, `CreatedByUserId (Customer)`

2. **Event / Bildirim**

* `ComplaintCreated` olayı tetiklenir.
* Notification hedefi: **PR rolü** (ya da “CustomerRelations” rolü)

3. **PR İrtibat akışı**

* PR dashboard: “Yeni Şikayetler” listesi
* PR bir şikayeti “Üstlen” der → şikayet `AssignedToUserId` set olur, status `InProgress`
* PR cevap yazar (internal not + müşteri mesajı)
* Müşteriye bildirim gider: “Şikayetiniz inceleniyor / cevaplandı”

4. **Mesajlaşma (birimler arası haberleşme şartına da hizmet eder)**

* `ComplaintMessage` tablosu:

  * `ComplaintId`, `SenderUserId`, `Message`, `IsInternal`, `CreatedAt`
* PR müşteriyle buradan yazışır (UI’da thread görünür)

5. **Yetkiler (RBAC)**

* Customer: sadece **kendi şikayetlerini** görür/açar
* PR: tüm şikayetleri görür, üstlenir, cevaplar
* Admin: hepsini görür

> Not: Senin “3 rol” şartın varsa, PR’ı mevcut 3 rolden biriyle eşleştiririz (ör. “Sales” ya da “Support” gibi). Ama idealde PR ayrı rol olur.

---

