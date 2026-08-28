import { useState, useEffect } from 'react';
import { apiClient } from '../services/apiClient';

interface Product {
    id: string;
    sku: string;
    barcode: string;
    name: string;
    categoryName: string;
    salePrice: number;
    stockCount: number;
}

export const ProductsPage = () => {
    const [products, setProducts] = useState<Product[]>([]);
    const [search, setSearch] = useState('');
    const [loading, setLoading] = useState(false);

    useEffect(() => {
        fetchProducts();
    }, [search]);

    const fetchProducts = async () => {
        setLoading(true);
        try {
            const res = await apiClient.get(`/api/products?search=${encodeURIComponent(search)}`);
            setProducts(res.data.items);
        } finally { setLoading(false); }
    };

    return (
        <div>
            <h1>Products</h1>
            <input type="text" placeholder="Search by SKU, name, barcode" value={search} onChange={e => setSearch(e.target.value)} />
            {loading ? (
                <p>Loading products...</p>
            ) : (
                <table>
                    <thead>
                    <tr><th>SKU</th><th>Name</th><th>Category</th><th>Price</th><th>Stock</th></tr>
                    </thead>
                    <tbody>
                    {products.map(p => (
                        <tr key={p.id}>
                            <td>{p.sku}</td>
                            <td>{p.name}</td>
                            <td>{p.categoryName}</td>
                            <td>${p.salePrice}</td>
                            <td>{p.stockCount}</td>
                        </tr>
                    ))}
                    </tbody>
                </table>
            )}
        </div>
    );
};