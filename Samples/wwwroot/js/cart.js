class Cart {
  constructor() {
    this.cartKey = 'shoppingCart';
    this.items = this.loadCart();
  }

  loadCart() {
    const storedCart = localStorage.getItem(this.cartKey);
    return storedCart ? JSON.parse(storedCart) : [];
  }

  saveCart() {
    localStorage.setItem(this.cartKey, JSON.stringify(this.items));
  }

  addItem(item) {
    // Check if item already exists, if so, increment quantity
    const existingItem = this.items.find(cartItem => cartItem.id === item.id);
    if (existingItem) {
      existingItem.quantity = (existingItem.quantity || 1) + 1;
    } else {
      item.quantity = 1; // Add quantity property
      this.items.push(item);
    }
    this.saveCart();
  }

  removeItem(itemId) {
    this.items = this.items.filter(item => item.id !== itemId);
    this.saveCart();
  }

  getItems() {
    return this.items;
  }

  getTotal() {
    return this.items.reduce((total, item) => total + (item.price * (item.quantity || 1)), 0);
  }

  getItemCount() {
     return this.items.reduce((count, item) => count + (item.quantity || 1), 0);
  }

  clearCart() {
    this.items = [];
    this.saveCart();
  }
}
