export const LANG_KEY = 'shopapi-lang'

export const i18n = {
  tr: {
    brand: 'Nova Market',
    subtitle: 'Premium ürünler, hızlı teslimat, güvenli alışveriş',
    loginTitle: 'Hesabına Giriş Yap',
    loginSub: 'Alışverişe devam etmek için oturum aç.',
    email: 'E-posta',
    password: 'Şifre',
    loginBtn: 'Giriş Yap',
    logout: 'Çıkış',
    shop: 'Mağaza',
    search: 'Ürün ara (telefon, parfüm, laptop...)',
    allCategories: 'Tüm kategoriler',
    popular: 'Popülerlik',
    rating: 'Puan',
    priceAsc: 'Fiyat (artan)',
    priceDesc: 'Fiyat (azalan)',
    discover: 'Keşfet',
    listed: 'ürün listelendi',
    addToCart: 'Sepete Ekle',
    cart: 'Sepetim',
    cartEmpty: 'Sepetin boş.',
    total: 'Toplam',
    clear: 'Sepeti Temizle',
    loading: 'Ürünler yükleniyor...',
    translating: 'Ürünler Türkçeye çevriliyor...',
    loginError: 'Giriş başarısız, bilgileri kontrol et.',
    authHint: 'Demo: admin@admin.local / Admin123!',
    loadError: 'Ürünler yüklenemedi.',
    freeShipping: '1500 TL üzeri kargo bedava',
    ratingLabel: 'puan',
  },
  en: {
    brand: 'Nova Market',
    subtitle: 'Premium products, fast delivery, secure checkout',
    loginTitle: 'Sign In',
    loginSub: 'Log in to continue shopping.',
    email: 'Email',
    password: 'Password',
    loginBtn: 'Sign In',
    logout: 'Logout',
    shop: 'Store',
    search: 'Search products...',
    allCategories: 'All categories',
    popular: 'Popularity',
    rating: 'Rating',
    priceAsc: 'Price low to high',
    priceDesc: 'Price high to low',
    discover: 'Discover',
    listed: 'products listed',
    addToCart: 'Add to Cart',
    cart: 'My Cart',
    cartEmpty: 'Your cart is empty.',
    total: 'Total',
    clear: 'Clear Cart',
    loading: 'Loading products...',
    translating: 'Translating products...',
    loginError: 'Login failed, check your credentials.',
    authHint: 'Demo: admin@admin.local / Admin123!',
    loadError: 'Could not load products.',
    freeShipping: 'Free shipping over $50',
    ratingLabel: 'rating',
  },
}

export function formatPrice(price, lang) {
  if (lang === 'tr') {
    const tryPrice = Math.round(price * 34.5)
    return `₺${tryPrice.toLocaleString('tr-TR')}`
  }
  return `$${price.toFixed(2)}`
}
