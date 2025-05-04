class Stock {
  id;
  title;
  description;
  price;

  async listStock() {
    const response = await fetch("/api/stock");
    const data = await response.json();
    const stockList = [];
    data.forEach((item) => {
      const stockItem = new Stock();
      stockItem.id = item.id;
      stockItem.title = item.title;
      stockItem.description = item.description;
      stockItem.price = item.price;
      stockList.push(stockItem);
    });
    return stockList;
  }

  async getStockById(id) {
    const response = await fetch(`/api/stock/${id}`);
    const data = await response.json();
    this.id = data.id;
    this.title = data.title;
    this.description = data.description;
    this.price = data.price;
  }
}
