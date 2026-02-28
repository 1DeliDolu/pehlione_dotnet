Anladım: **admin oluşturulduğu anda**, sistemdeki **tüm çalışan hesaplarına** (admin hariç) “Yeni admin hesabı oluşturuldu” gibi bir **broadcast bildirim** düşsün.

Bunu sağlamanın en sağlam 2 yolu var:

## Seçenek A (Hızlı, prototip için yeterli): “AllEmployees” hedef tipi

Notification tablosuna bir hedefleme alanı ekleriz:

* `TargetType`: `Role | User | AllEmployees`
* `AllEmployees` seçilince, bildirim listelerken:

  * Admin dışındaki rollere sahip kullanıcılar bu bildirimi görür
  * Admin de isterse ayrıca “hepsini gör” ekranından görür (admin zaten her şeyi görüyor)

**Artı:** tek kayıt, performans iyi
**Eksi:** “kimin okuduğunu tek tek takip” zorlaşır (okundu bilgisi kullanıcı bazlı olmalı)

Bunu çözmek için okundu bilgisini ayrı tabloda tutarız:

* `NotificationReads(NotificationId, UserId, ReadAt)`

## Seçenek B (Kurumsal/izlenebilir): Recipient tablosu ile fan-out

Admin yaratılınca:

* 1 adet `Notification`
* Her çalışan için `NotificationRecipient(NotificationId, UserId, IsRead, ReadAt)` satırı

**Artı:** kim okudu/okumadı net
**Eksi:** çalışan sayısı büyürse insert sayısı artar (ama prototipte sorun değil)

---

## Önerim

Senin “dashboard iş akışı” + “admin tüm bildirimleri görsün” hedefinle en temiz çözüm:

* **Notification (tek kayıt)** + **NotificationRecipient (kime gitti)** yaklaşımı (Seçenek B)
* Böylece hem “her bir çalışana gitsin” net olur, hem de “okundu/okunmadı” raporu çıkar.

---

## Akış (Admin created → herkesin dashboard’una bildirim)

1. `AdminCreated` olayı oluşur (event/outbox veya doğrudan servis)
2. NotificationService:

   * Sistemdeki tüm “çalışan kullanıcıları” (Admin hariç) listeler
   * Her biri için `NotificationRecipient` üretir
3. Kullanıcı kendi dashboard’una girince:

   * “Kendi recipient kayıtları” üzerinden bildirimler listelenir

---


