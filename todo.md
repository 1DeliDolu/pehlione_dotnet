Aşağıda daha temiz, daha premium görünen ama **yalnızca Bootstrap class’ları** kullanan sadeleştirilmiş sürüm var.
Özel CSS yok. `form-select`, `form-select-lg`, `form-control-lg`, `card`, `shadow-sm`, `rounded-4`, `table`, `badge` gibi Bootstrap sınıflarıyla ilerliyor.

```cshtml
@model Pehlione.Models.ViewModels.Staff.ReceiveStockVm
@{
    ViewData["Title"] = "Stok Girişi";
    var subCategoriesUrl = Url.Action("SubCategories", "Inventory", new { area = "Staff" }) ?? "/Staff/Inventory/SubCategories";
    var productsByCategoryUrl = Url.Action("ProductsByCategory", "Inventory", new { area = "Staff" }) ?? "/Staff/Inventory/ProductsByCategory";
}

<div class="container py-4">
    <div class="card border-0 shadow-sm rounded-4 mb-4">
        <div class="card-body p-4">
            <div class="d-flex flex-column flex-lg-row align-items-lg-center justify-content-between gap-3">
                <div>
                    <span class="badge text-bg-primary mb-2">Inventory</span>
                    <h1 class="h3 mb-1">Stok Girişi</h1>
                    <p class="text-muted mb-0">
                        Kategori ve ürün seçerek stok ekleyin. Girilen miktar mevcut stok üzerine ilave edilir.
                    </p>
                </div>
                <div class="text-muted small">
                    Personel paneli / Stok yönetimi
                </div>
            </div>
        </div>
    </div>

    @if (TempData["InventorySuccess"] is string ok && !string.IsNullOrWhiteSpace(ok))
    {
        <div class="alert alert-success shadow-sm">@ok</div>
    }

    @if (TempData["InventoryError"] is string err && !string.IsNullOrWhiteSpace(err))
    {
        <div class="alert alert-warning shadow-sm">@err</div>
    }

    <div class="row g-4">
        <div class="col-12 col-lg-7">
            <form asp-area="Staff" asp-controller="Inventory" asp-action="Receive" method="post" class="card border-0 shadow-sm rounded-4 h-100">
                @Html.AntiForgeryToken()

                <div class="card-header bg-white border-0 pt-4 px-4">
                    <h2 class="h5 mb-1">Satın Alma Girişi</h2>
                    <p class="text-muted small mb-0">Ürün seçip miktar belirleyerek stok giriş işlemi yapın.</p>
                </div>

                <div class="card-body px-4 pb-4">
                    <div asp-validation-summary="ModelOnly" class="text-danger mb-3"></div>

                    <div class="row g-3">
                        <div class="col-12">
                            <label asp-for="TopCategoryId" class="form-label fw-semibold"></label>
                            <select asp-for="TopCategoryId"
                                    asp-items="Model.TopCategoryOptions"
                                    class="form-select form-select-lg"
                                    id="topCategorySelect"></select>
                            <span asp-validation-for="TopCategoryId" class="text-danger small"></span>
                        </div>

                        <div class="col-12 col-md-6">
                            <label asp-for="SubCategoryId" class="form-label fw-semibold"></label>
                            <select asp-for="SubCategoryId"
                                    class="form-select form-select-lg"
                                    id="subCategorySelect">
                                <option value="">Seçiniz</option>
                                @foreach (var c in Model.SubCategoryOptions)
                                {
                                    if (Model.SubCategoryId.HasValue && c.Value == Model.SubCategoryId.Value.ToString())
                                    {
                                        <option value="@c.Value" selected>@c.Text</option>
                                    }
                                    else
                                    {
                                        <option value="@c.Value">@c.Text</option>
                                    }
                                }
                            </select>
                            <span asp-validation-for="SubCategoryId" class="text-danger small"></span>
                        </div>

                        <div class="col-12 col-md-6">
                            <label asp-for="SubSubCategoryId" class="form-label fw-semibold"></label>
                            <select asp-for="SubSubCategoryId"
                                    class="form-select form-select-lg"
                                    id="subSubCategorySelect">
                                <option value="">Seçiniz</option>
                                @foreach (var c in Model.SubSubCategoryOptions)
                                {
                                    if (Model.SubSubCategoryId.HasValue && c.Value == Model.SubSubCategoryId.Value.ToString())
                                    {
                                        <option value="@c.Value" selected>@c.Text</option>
                                    }
                                    else
                                    {
                                        <option value="@c.Value">@c.Text</option>
                                    }
                                }
                            </select>
                            <span asp-validation-for="SubSubCategoryId" class="text-danger small"></span>
                        </div>

                        <div class="col-12">
                            <label asp-for="ProductId" class="form-label fw-semibold"></label>
                            <select asp-for="ProductId"
                                    class="form-select form-select-lg"
                                    id="productSelect">
                                @foreach (var p in Model.ProductOptions)
                                {
                                    if (p.Value == Model.ProductId.ToString())
                                    {
                                        <option value="@p.Value" selected>@p.Text</option>
                                    }
                                    else
                                    {
                                        <option value="@p.Value">@p.Text</option>
                                    }
                                }
                            </select>
                            <span asp-validation-for="ProductId" class="text-danger small"></span>
                        </div>

                        <div class="col-12">
                            <label asp-for="Quantity" class="form-label fw-semibold"></label>
                            <input asp-for="Quantity" class="form-control form-control-lg" min="1" placeholder="Miktar giriniz" />
                            <span asp-validation-for="Quantity" class="text-danger small"></span>
                        </div>
                    </div>

                    <div class="d-flex justify-content-end mt-4">
                        <button type="submit" class="btn btn-primary btn-lg px-4">
                            Stok Girişi Yap
                        </button>
                    </div>
                </div>
            </form>
        </div>

        <div class="col-12 col-lg-5">
            @if (User.IsInRole("IT") || User.IsInRole("Admin"))
            {
                <form asp-area="Staff" asp-controller="Inventory" asp-action="DeleteProduct" method="post" class="card border-0 shadow-sm rounded-4">
                    @Html.AntiForgeryToken()

                    <div class="card-header bg-white border-0 pt-4 px-4">
                        <h2 class="h5 mb-1">IT İşlem: Ürün Sil</h2>
                        <p class="text-muted small mb-0">Yalnızca yetkili kullanıcılar bu işlemi yapabilir.</p>
                    </div>

                    <div class="card-body px-4 pb-4">
                        <div class="mb-3">
                            <label class="form-label fw-semibold" for="productIdDelete">Ürün</label>
                            <select id="productIdDelete" name="productId" class="form-select form-select-lg">
                                @foreach (var p in Model.AllProducts)
                                {
                                    <option value="@p.ProductId">@p.Name (@p.Sku)</option>
                                }
                            </select>
                        </div>

                        <div class="d-flex justify-content-end">
                            <button type="submit" class="btn btn-outline-danger btn-lg px-4">
                                Ürünü Sil
                            </button>
                        </div>
                    </div>
                </form>
            }
        </div>
    </div>

    <div class="card border-0 shadow-sm rounded-4 mt-4">
        <div class="card-header bg-white border-0 pt-4 px-4">
            <h2 class="h5 mb-1">Güncel Stok Durumu</h2>
            <p class="text-muted small mb-0">Ürünlerin mevcut stok miktarları</p>
        </div>
        <div class="card-body px-4 pb-4">
            @if (Model.StockSnapshots.Count == 0)
            {
                <div class="alert alert-info mb-0">Stok verisi yok.</div>
            }
            else
            {
                <div class="table-responsive">
                    <table class="table table-hover align-middle mb-0">
                        <thead class="table-light">
                            <tr>
                                <th>Ürün</th>
                                <th>SKU</th>
                                <th class="text-end">Stok</th>
                            </tr>
                        </thead>
                        <tbody>
                            @foreach (var s in Model.StockSnapshots)
                            {
                                <tr>
                                    <td>@s.ProductName</td>
                                    <td><code>@s.Sku</code></td>
                                    <td class="text-end fw-semibold">@s.Quantity</td>
                                </tr>
                            }
                        </tbody>
                    </table>
                </div>
            }
        </div>
    </div>

    <div class="card border-0 shadow-sm rounded-4 mt-4">
        <div class="card-header bg-white border-0 pt-4 px-4">
            <h2 class="h5 mb-1">Son Stok Hareketleri</h2>
            <p class="text-muted small mb-0">En son stok giriş ve çıkış kayıtları</p>
        </div>
        <div class="card-body px-4 pb-4">
            @if (Model.RecentMovements.Count == 0)
            {
                <div class="alert alert-info mb-0">Hareket kaydı yok.</div>
            }
            else
            {
                <div class="table-responsive">
                    <table class="table table-hover align-middle mb-0">
                        <thead class="table-light">
                            <tr>
                                <th>Tarih</th>
                                <th>Ürün</th>
                                <th>Tip</th>
                                <th class="text-end">Adet</th>
                                <th>Açıklama</th>
                            </tr>
                        </thead>
                        <tbody>
                            @foreach (var m in Model.RecentMovements)
                            {
                                <tr>
                                    <td>@m.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")</td>
                                    <td>@m.ProductName <code>@m.Sku</code></td>
                                    <td>
                                        @if (m.Type == "In")
                                        {
                                            <span class="badge text-bg-success">In</span>
                                        }
                                        else
                                        {
                                            <span class="badge text-bg-danger">Out</span>
                                        }
                                    </td>
                                    <td class="text-end fw-semibold">@m.Quantity</td>
                                    <td>@(string.IsNullOrWhiteSpace(m.Reason) ? "-" : m.Reason)</td>
                                </tr>
                            }
                        </tbody>
                    </table>
                </div>
            }
        </div>
    </div>
</div>

@section Scripts {
    <partial name="_ValidationScriptsPartial" />
    <script>
        (function () {
            const topCategorySelect = document.getElementById('topCategorySelect');
            const subCategorySelect = document.getElementById('subCategorySelect');
            const subSubCategorySelect = document.getElementById('subSubCategorySelect');
            const productSelect = document.getElementById('productSelect');
            if (!topCategorySelect || !subCategorySelect || !subSubCategorySelect || !productSelect) return;

            const subCategoriesUrl = '@subCategoriesUrl';
            const productsByCategoryUrl = '@productsByCategoryUrl';

            function fillOptions(select, items, valueField, textField, selectedValue, includeEmpty) {
                select.innerHTML = '';
                if (includeEmpty) {
                    const emptyOption = document.createElement('option');
                    emptyOption.value = '';
                    emptyOption.textContent = 'Seçiniz';
                    select.appendChild(emptyOption);
                }
                for (const item of items) {
                    const option = document.createElement('option');
                    option.value = String(item[valueField]);
                    option.textContent = item[textField];
                    if (String(item[valueField]) === String(selectedValue || '')) {
                        option.selected = true;
                    }
                    select.appendChild(option);
                }
                if (select.options.length > 0 && select.selectedIndex < 0) {
                    select.selectedIndex = 0;
                }
            }

            async function loadSubCategories(parentId, targetSelect, selectedValue) {
                if (!parentId) {
                    fillOptions(targetSelect, [], 'value', 'text', '', true);
                    return;
                }

                const response = await fetch(`${subCategoriesUrl}?parentId=${encodeURIComponent(parentId)}`, {
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });

                if (!response.ok) {
                    fillOptions(targetSelect, [], 'value', 'text', '', true);
                    return;
                }

                const items = await response.json();
                fillOptions(targetSelect, items, 'value', 'text', selectedValue, true);
            }

            async function loadProducts(categoryId, selectedValue) {
                if (!categoryId) {
                    fillOptions(productSelect, [], 'value', 'text', '', false);
                    return;
                }

                const response = await fetch(`${productsByCategoryUrl}?categoryId=${encodeURIComponent(categoryId)}`, {
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });

                if (!response.ok) {
                    fillOptions(productSelect, [], 'value', 'text', '', false);
                    return;
                }

                const items = await response.json();
                fillOptions(productSelect, items, 'value', 'text', selectedValue, false);
            }

            async function syncProducts() {
                const topId = parseInt(topCategorySelect.value || '0', 10);
                const subId = parseInt(subCategorySelect.value || '0', 10);
                const subSubId = parseInt(subSubCategorySelect.value || '0', 10);
                const selectedNode = subSubId || subId || topId;
                const currentProduct = productSelect.value || '';
                await loadProducts(selectedNode, currentProduct);
            }

            topCategorySelect.addEventListener('change', async function () {
                await loadSubCategories(parseInt(topCategorySelect.value || '0', 10), subCategorySelect, '');
                await loadSubCategories(parseInt(subCategorySelect.value || '0', 10), subSubCategorySelect, '');
                await syncProducts();
            });

            subCategorySelect.addEventListener('change', async function () {
                await loadSubCategories(parseInt(subCategorySelect.value || '0', 10), subSubCategorySelect, '');
                await syncProducts();
            });

            subSubCategorySelect.addEventListener('change', async function () {
                await syncProducts();
            });

            (async function initialize() {
                await loadSubCategories(parseInt(topCategorySelect.value || '0', 10), subCategorySelect, '@(Model.SubCategoryId?.ToString() ?? "")');
                await loadSubCategories(parseInt(subCategorySelect.value || '0', 10), subSubCategorySelect, '@(Model.SubSubCategoryId?.ToString() ?? "")');
                await loadProducts(parseInt(subSubCategorySelect.value || subCategorySelect.value || topCategorySelect.value || '0', 10), '@Model.ProductId');
            })();
        })();
    </script>
}
```

Bir de daha iyi görünmesi için şu Bootstrap tercihlerini kullandım:

* form alanları: `form-select form-select-lg`, `form-control form-control-lg`
* kartlar: `card border-0 shadow-sm rounded-4`
* tablo: `table table-hover align-middle`
* başlık alanı: `badge + h3 + text-muted`
* butonlar: `btn btn-primary btn-lg`, `btn btn-outline-danger btn-lg`

İstersen bir sonraki mesajda bunu bir tık daha düzenleyip:

* **2 select yan yana**
* **ürün ve miktar aynı satırda**
* **daha kompakt ama modern Bootstrap layout**

olarak da verebilirim.
