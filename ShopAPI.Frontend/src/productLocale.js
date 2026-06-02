const categoryTr = {
  smartphones: 'Akıllı Telefon',
  laptops: 'Laptop',
  fragrances: 'Parfüm',
  skincare: 'Cilt Bakımı',
  groceries: 'Market',
  'home-decoration': 'Ev Dekorasyonu',
  furniture: 'Mobilya',
  tops: 'Üst Giyim',
  'womens-dresses': 'Kadın Elbise',
  'womens-shoes': 'Kadın Ayakkabı',
  'mens-shirts': 'Erkek Gömlek',
  'mens-shoes': 'Erkek Ayakkabı',
  'mens-watches': 'Erkek Saat',
  'womens-watches': 'Kadın Saat',
  'womens-bags': 'Kadın Çanta',
  sunglasses: 'Güneş Gözlüğü',
  automotive: 'Otomotiv',
  motorcycle: 'Motosiklet',
  lighting: 'Aydınlatma',
  beauty: 'Güzellik',
  sports: 'Spor',
  tablets: 'Tablet',
  mobile: 'Mobil Aksesuar',
  electronics: 'Elektronik',
  gaming: 'Oyun',
  home: 'Ev & Yaşam',
}

const productTitleTr = {
  'Wireless Mouse': 'Kablosuz Mouse',
  'Mechanical Keyboard': 'Mekanik Klavye',
  'Gaming Headset': 'Oyuncu Kulaklığı',
  'Desk Lamp': 'Masa Lambası',
  '27" 4K Monitor': '27" 4K Monitör',
  'USB-C Hub': 'USB-C Çoklayıcı',
  'Portable SSD 1TB': 'Taşınabilir SSD 1TB',
  'Ergonomic Chair': 'Ergonomik Sandalye',
  'Coffee Maker': 'Kahve Makinesi',
  'Air Fryer': 'Air Fryer',
  'Smart Bulb': 'Akıllı Ampul',
  'Gaming Mousepad': 'Oyuncu Mousepad',
  'Webcam Full HD': 'Webcam Full HD',
  'Bluetooth Speaker': 'Bluetooth Hoparlör',
  'Noise Cancelling Earbuds': 'Gürültü Engelleyen Kulaklık',
  'Office Desk': 'Ofis Masası',
  'Robot Vacuum': 'Robot Süpürge',
  'Smart Plug': 'Akıllı Priz',
  'Mechanical Number Pad': 'Mekanik Numpad',
  'Laptop Stand': 'Laptop Standı',
}

export function localizeCategory(category, lang) {
  if (lang === 'en') return category
  return categoryTr[category] ?? category.replace(/-/g, ' ')
}

const descTr = 'Hızlı kargo ve güvenli alışveriş ile kapında.'

export async function localizeProducts(products, lang) {
  if (lang === 'en') {
    return products.map((p) => ({
      ...p,
      displayTitle: p.title,
      displayDescription: p.description,
      displayCategory: p.category,
    }))
  }

  return products.map((p) => ({
      ...p,
      displayTitle: productTitleTr[p.title] ?? p.title,
      displayDescription: descTr,
      displayCategory: localizeCategory(p.category, 'tr'),
    }))
}
