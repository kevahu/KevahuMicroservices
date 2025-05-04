window.addEventListener('load', async () => {
    const stock = new Stock();
    const cart = new Cart();
    const stockItemsContainer = document.getElementById('stock-items');
    const cartCountElement = document.getElementById('cart-count');
    const stockItemTemplate = document.getElementById('stock-item-template');

    function updateCartCount() {
        const count = cart.getItemCount();
        if (count > 0) {
            cartCountElement.textContent = count;
            cartCountElement.style.display = 'inline-block';
        } else {
            cartCountElement.style.display = 'none';
        }
    }

    async function loadStockItems() {
        try {
            const items = await stock.listStock();
            stockItemsContainer.innerHTML = ''; // Clear existing items

            if (!stockItemTemplate) {
                console.error("Stock item template not found!");
                stockItemsContainer.innerHTML = '<p class="text-danger">Error displaying items: Template missing.</p>';
                return;
            }

            items.forEach(item => {
                const templateClone = stockItemTemplate.content.cloneNode(true);

                // Populate template elements
                templateClone.querySelector('.stock-item-img').src = `https://picsum.photos/300/200?random=${item.id}`;
                templateClone.querySelector('.stock-item-img').alt = item.title;
                templateClone.querySelector('.stock-item-title').textContent = item.title;
                templateClone.querySelector('.stock-item-description').textContent = item.description;
                templateClone.querySelector('.stock-item-price').textContent = item.price.toFixed(2);

                const buttonElement = templateClone.querySelector('.add-to-cart-btn');
                buttonElement.setAttribute('data-item-id', item.id);
                buttonElement.setAttribute('data-item-title', item.title);
                buttonElement.setAttribute('data-item-price', item.price);

                // Add event listener directly to the cloned button
                buttonElement.addEventListener('click', (event) => {
                    const clickedButton = event.target; // Use event.target which is the button itself
                    const itemToAdd = {
                        id: clickedButton.getAttribute('data-item-id'),
                        title: clickedButton.getAttribute('data-item-title'),
                        price: parseFloat(clickedButton.getAttribute('data-item-price'))
                    };
                    cart.addItem(itemToAdd);
                    updateCartCount();
                    // Optional: Add visual feedback
                    clickedButton.textContent = 'Added!';
                    clickedButton.disabled = true; // Disable after adding
                    setTimeout(() => {
                        clickedButton.textContent = 'Add to Cart';
                        clickedButton.disabled = false; // Re-enable
                     }, 1000);
                });

                stockItemsContainer.appendChild(templateClone);
            });

        } catch (error) {
            console.error("Failed to load stock items:", error);
            stockItemsContainer.innerHTML = '<p class="text-danger">Could not load items. Please try again later.</p>';
        }
    }

    // Initial load
    await loadStockItems();
    updateCartCount();
});