export function mapApiProduct(product) {
  const variants = (product.variants ?? []).map((v) => ({
    id: v.id,
    name: v.name,
    price: Number(v.overridePrice ?? product.price ?? 0),
    stock: v.stock ?? 0,
  }))
  return {
    id: product.id,
    title: product.name,
    description: `Stock: ${product.stock}`,
    category: product.category?.slug ?? 'general',
    categoryName: product.category?.name ?? 'General',
    categoryId: product.categoryId,
    price: Number(product.price ?? 0),
    isActive: product.isActive,
    rating: Number((4.2 + ((product.stock ?? 0) % 8) / 10).toFixed(1)),
    stock: product.stock ?? 0,
    variants,
    thumbnail: `https://picsum.photos/seed/${product.id}/480/320`,
  }
}

export function cartItemUnitPrice(item) {
  return Number(item.productVariant?.overridePrice ?? item.product?.price ?? 0)
}
