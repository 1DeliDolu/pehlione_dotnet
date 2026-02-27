### A) Hedef

Az önce oluşturduğumuz **Identity migration**’ını MySQL’e uygulayıp, veritabanında **AspNet*** tablolarının oluştuğunu doğrulayacağız.

### B) CLI Komutu (maks. 1 komut)

```bash
dotnet ef database update
```

### C) Dosya Değişiklikleri (tam içerik)

Bu adım **kod/dosya değiştirmez**. Sadece migration DB’ye uygulanır.

### D) Kısa Açıklama (en fazla 5 madde)

* Komut başarıyla biterse MySQL’de `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles` vb. tablolar oluşmuş olur.
* Böylece roller ve kullanıcılar artık DB’de tutulabilir.
* Bir sonraki adımda **rol seed** (Purchasing/IT/Admin) ve **dev test kullanıcıları** oluşturacağız.
* Ardından Inventory ekranında: Purchasing **+Arttır** görür ama **Sil** görmez; IT **Sil** görür ama **+Arttır** görmez.
* En sonda endpoint seviyesinde de (403) doğrulayacağız.

### E) Git Commit

**Commit mesajı:** `Apply Identity migration to database`

```bash
git add -A && git commit -m "Apply Identity migration to database"
```

“**bitti**” yazınca sıradaki adım: **Role + user seed** (startup’ta otomatik) ekleyeceğim.
