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

const translationCache = new Map()

async function translateEnToTr(text) {
  if (!text?.trim()) return text
  const key = text.slice(0, 200)
  if (translationCache.has(key)) return translationCache.get(key)

  try {
    const url = `https://api.mymemory.translated.net/get?q=${encodeURIComponent(text.slice(0, 500))}&langpair=en|tr`
    const res = await fetch(url)
    const data = await res.json()
    const translated = data.responseData?.translatedText ?? text
    translationCache.set(key, translated)
    return translated
  } catch {
    return text
  }
}

function delay(ms) {
  return new Promise((r) => setTimeout(r, ms))
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

  const result = []
  for (let i = 0; i < products.length; i++) {
    const p = products[i]
    const displayTitle = await translateEnToTr(p.title)

    result.push({
      ...p,
      displayTitle,
      displayDescription: descTr,
      displayCategory: localizeCategory(p.category, 'tr'),
    })

    if (i % 4 === 3) await delay(80)
  }

  return result
}
