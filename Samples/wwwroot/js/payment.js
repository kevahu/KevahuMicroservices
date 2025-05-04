class Payment {
  stockIds = [];
  cardNumber;
  cardHolderName;
  cardExpiryDate;
  cardCVV;

  async processPayment() {
    const response = await fetch("/api/payment", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(this),
    });
    const data = await response.json();
    // data: { "success": boolean, "message": string }
    return data;
  }
}
