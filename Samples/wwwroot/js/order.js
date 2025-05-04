window.addEventListener('load', () => {
    const cart = new Cart();
    const payment = new Payment();
    const orderItemsList = document.getElementById('order-items');
    const orderTotalElement = document.getElementById('order-total');
    const paymentForm = document.getElementById('payment-form');
    const paymentStatusModalElement = document.getElementById('paymentStatusModal');
    const paymentStatusModal = new bootstrap.Modal(paymentStatusModalElement);
    const paymentStatusBody = document.getElementById('paymentStatusBody');
    const modalCloseButton = document.getElementById('modalCloseButton');
    const modalHomeButton = document.getElementById('modalHomeButton');
    const orderItemTemplate = document.getElementById('order-item-template'); // Get template

    function removeItemFromOrder(itemId) {
        cart.removeItem(itemId);
        displayOrderSummary(); // Refresh the order summary
    }

    function displayOrderSummary() {
        const items = cart.getItems();
        orderItemsList.innerHTML = ''; // Clear existing items

        if (!orderItemTemplate) {
             console.error("Order item template not found!");
             orderItemsList.innerHTML = '<li class="list-group-item text-danger">Error displaying cart: Template missing.</li>';
             return;
        }

        if (items.length === 0) {
            orderItemsList.innerHTML = '<li class="list-group-item">Your cart is empty.</li>';
            orderTotalElement.textContent = '0.00';
            // Disable payment form if cart is empty
            paymentForm.querySelectorAll('input:not([type=hidden]), button').forEach(el => el.disabled = true);
            return;
        }

        items.forEach(item => {
            const templateClone = orderItemTemplate.content.cloneNode(true);
            const quantity = item.quantity || 1;
            const totalPrice = (item.price * quantity).toFixed(2);

            // Populate template elements
            templateClone.querySelector('.order-item-title').textContent = `${item.title} (x${quantity})`;
            templateClone.querySelector('.order-item-price-each').textContent = item.price.toFixed(2);
            templateClone.querySelector('.order-item-price-total').textContent = `$${totalPrice}`;

            const removeButton = templateClone.querySelector('.remove-item-btn');
            removeButton.setAttribute('data-item-id', item.id);

            // Add event listener directly to the cloned button
            removeButton.addEventListener('click', (event) => {
                const itemId = event.currentTarget.getAttribute('data-item-id');
                removeItemFromOrder(itemId);
            });

            orderItemsList.appendChild(templateClone);
        });

        const total = cart.getTotal();
        orderTotalElement.textContent = total.toFixed(2);
        // Ensure form is enabled if cart has items
        paymentForm.querySelectorAll('input:not([type=hidden]), button').forEach(el => el.disabled = false);
    }

    paymentForm.addEventListener('submit', async (event) => {
        event.preventDefault();

        // Basic HTML5 validation check
        if (!paymentForm.checkValidity()) {
            event.stopPropagation();
            paymentForm.classList.add('was-validated'); // Trigger Bootstrap validation styles
            return;
        }
        paymentForm.classList.remove('was-validated'); // Reset validation styles if previously shown

        // Disable button to prevent multiple submissions
        const submitButton = paymentForm.querySelector('button[type="submit"]');
        submitButton.disabled = true;
        submitButton.innerHTML = '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> Processing...';

        // Populate payment details
        const cartItems = cart.getItems();
        payment.stockIds = cartItems.flatMap(item => Array(item.quantity || 1).fill(item.id)); // Get all individual IDs based on quantity
        payment.cardNumber = document.getElementById('cardNumber').value;
        payment.cardHolderName = document.getElementById('cardHolderName').value;
        payment.cardExpiryDate = document.getElementById('cardExpiryDate').value;
        payment.cardCVV = document.getElementById('cardCVV').value;

        try {
            const result = await payment.processPayment();
            // result: { "success": boolean, "message": string }

            if (result.success) {
                paymentStatusBody.textContent = result.message || 'Payment successful!';
                paymentStatusBody.className = 'modal-body text-success';
                modalHomeButton.style.display = 'inline-block';
                modalCloseButton.style.display = 'none';
                cart.clearCart(); // Clear cart on successful payment
                displayOrderSummary(); // Update display after clearing cart
            } else {
                paymentStatusBody.textContent = result.message || 'Payment failed. Please try again.';
                paymentStatusBody.className = 'modal-body text-danger';
                modalHomeButton.style.display = 'none';
                modalCloseButton.style.display = 'inline-block';
            }
        } catch (error) {
            console.error("Payment processing error:", error);
            paymentStatusBody.textContent = 'An error occurred during payment processing. Please try again later.';
            paymentStatusBody.className = 'modal-body text-danger';
            modalHomeButton.style.display = 'none';
            modalCloseButton.style.display = 'inline-block';
        } finally {
             // Re-enable button
            submitButton.disabled = false;
            submitButton.innerHTML = 'Pay Now';
            paymentStatusModal.show();
        }
    });

    // Initial display
    displayOrderSummary();
});
