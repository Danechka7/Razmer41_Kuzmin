    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;
    using static System.Net.Mime.MediaTypeNames;

    namespace Razmer41_Kuzmin
    {
        public partial class OrderWindow : Window
        {
            List<OrderProduct> selectedOrderProducts = new List<OrderProduct>();
            List<Product> selectedProducts = new List<Product>();
            private Order currentOrder = new Order();
            private OrderProduct currentOrderProduct = new OrderProduct();
            public OrderWindow(List<OrderProduct> selectedOrderProducts, List<Product> selectedProducts, string FIO)
            {
                InitializeComponent();
                DataContext = currentOrder;

                var currentPickups = Kuzmin41Entities.GetContext().PickUpPoint.ToList();
                PickUpPointComboBox.ItemsSource = currentPickups;

                FIOBox.Text = FIO;

                var Users = Kuzmin41Entities.GetContext().User.ToList();
                if (FIO != "Гость")
                {
                    User ID = Users.FirstOrDefault(u => u.UserSurname + " " + u.UserName + " " + u.UserPatronymic == FIO);
                    currentOrder.OrderClientID = ID.UserID;
                }
                OrderListView.ItemsSource = selectedProducts;
                OrderNumBox.Text = selectedOrderProducts.First().OrderID.ToString();
                foreach (Product p in selectedProducts)
                {
                    p.ProductRealStock = p.ProductQuantityInStock;

                    foreach (OrderProduct q in selectedOrderProducts)
                    {
                        if (p.ProductArticleNumber == q.ProductArticleNumber)
                            p.ProductRealStock = q.ProductQuantity;
                    }
                }

                var OP = Kuzmin41Entities.GetContext().OrderProduct.ToList();
                int Num = 0;

                foreach (OrderProduct q in OP)
                {
                    if (q.OrderID > Num)
                        Num = q.OrderID;
                }

                currentOrder.OrderID = 0;
                var Or = Kuzmin41Entities.GetContext().Order.ToList();
                foreach (Order o in Or)
                {
                    if (o.OrderID >= currentOrder.OrderID)
                        currentOrder.OrderID = o.OrderID + 1;
                }

                OrderNumBox.Text = (++Num).ToString();


                this.selectedOrderProducts = selectedOrderProducts;
                this.selectedProducts = selectedProducts;

                OrderFormDate.Text = DateTime.Now.ToString();

                SetDeliveryDate();
            }

            private void BtnPlus_Click(object sender, RoutedEventArgs e)
            {
                var prod = (sender as Button).DataContext as Product;

                prod.ProductRealStock++;

                var selectedOP = selectedOrderProducts.FirstOrDefault(p => p.ProductArticleNumber == prod.ProductArticleNumber);

                int index = selectedOrderProducts.IndexOf(selectedOP);

                selectedOrderProducts[index].ProductQuantity++;

                SetDeliveryDate();
                OrderListView.Items.Refresh();
            }

            private void BtnMinus_Click(object sender, RoutedEventArgs e)
            {
                var prod = (sender as Button).DataContext as Product;

                if (prod.ProductRealStock > 0)
                {
                    prod.ProductRealStock--;

                    var selectedOP = selectedOrderProducts.FirstOrDefault(p => p.ProductArticleNumber == prod.ProductArticleNumber);

                    int index = selectedOrderProducts.IndexOf(selectedOP);

                    selectedOrderProducts[index].ProductQuantity--;

                    SetDeliveryDate();

                    if (prod.ProductRealStock == 0)
                        DeleteBtn_Click(sender, e);

                    OrderListView.Items.Refresh();
                }
            }

            private void PickUpPointComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
            {
                currentOrder.OrderPickupPoint = PickUpPointComboBox.SelectedIndex + 1;
            }

            private void OrderSaveBtn_Click(object sender, RoutedEventArgs e)
            {
                StringBuilder errors = new StringBuilder();

                if (selectedProducts.Count == 0)
                    errors.AppendLine("Ваш заказ пустой");
                if (PickUpPointComboBox.SelectedIndex == -1)
                    errors.AppendLine("Выберите пункт выдачи");

                if (errors.Length > 0)
                {
                    MessageBox.Show(errors.ToString());
                    return;
                }

                try
                {
                    // Создаем новый заказ
                    Order newOrder = new Order
                    {
                        OrderID = Convert.ToInt32(OrderNumBox.Text),
                        OrderDate = Convert.ToDateTime(OrderFormDate.Text),
                        OrderStatus = "Новый",
                        OrderPickupPoint = ((PickUpPoint)PickUpPointComboBox.SelectedItem).PickupPointID
                    };

                    // Устанавливаем ID клиента, если не гость
                    if (FIOBox.Text != "Гость")
                    {
                        // Находим пользователя по ФИО
                        var user = Kuzmin41Entities.GetContext().User.FirstOrDefault(
                            u => (u.UserSurname + " " + u.UserName + " " + u.UserPatronymic) == FIOBox.Text);
                        if (user != null)
                        {
                            newOrder.OrderClientID = user.UserID;
                        }
                    }

                    // Добавляем заказ
                    Kuzmin41Entities.GetContext().Order.Add(newOrder);
                    Kuzmin41Entities.GetContext().SaveChanges(); // Сохраняем, чтобы получить OrderID

                    // Создаем список OrderProduct
                    var orderProductsToAdd = new List<OrderProduct>();

                    foreach (Product p in selectedProducts)
                    {
                        // Находим соответствующее количество из selectedOrderProducts
                        var existingOrderProduct = selectedOrderProducts
                            .FirstOrDefault(op => op.ProductArticleNumber == p.ProductArticleNumber);

                        int quantity = existingOrderProduct?.ProductQuantity ?? 1;

                        var orderProduct = new OrderProduct
                        {
                            OrderID = newOrder.OrderID,
                            ProductArticleNumber = p.ProductArticleNumber,
                            ProductQuantity = quantity
                        };

                        orderProductsToAdd.Add(orderProduct);

                        // Обновляем остатки
                        var productInDb = Kuzmin41Entities.GetContext().Product
                            .FirstOrDefault(prod => prod.ProductArticleNumber == p.ProductArticleNumber);

                        if (productInDb != null)
                        {
                            productInDb.ProductQuantityInStock -= quantity;
                        }
                    }

                    // Добавляем все товары заказа
                    Kuzmin41Entities.GetContext().OrderProduct.AddRange(orderProductsToAdd);
                    Kuzmin41Entities.GetContext().SaveChanges();

                    MessageBox.Show("Информация сохранена");

                    // Очищаем списки
                    selectedProducts.Clear();
                    selectedOrderProducts.Clear();

                    // ОЧЕНЬ ВАЖНО: Устанавливаем DialogResult = true
                    this.DialogResult = true;

                    // Закрываем окно
                    this.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message.ToString());
                    if (ex.InnerException != null)
                    {
                        MessageBox.Show(ex.InnerException.ToString());
                    }
                }
            }

            private void OrderFormDate_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
            {
                SetDeliveryDate();
            }

            private void DeleteBtn_Click(object sender, RoutedEventArgs e)
            {
                var prod = (sender as Button).DataContext as Product;

                if (MessageBox.Show("Вы точно хотите убрать товар из заказа?", "", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    prod.ProductQuantityInStock = prod.ProductRealStock;

                    selectedProducts.Remove(prod);
                }
                else if (prod.ProductRealStock == 0)
                    prod.ProductQuantityInStock = 1;
                SetDeliveryDate();
                OrderListView.Items.Refresh();
            }
            private void SetDeliveryDate()
            {
                int more = 0;
                foreach (Product p in selectedProducts)
                {
                    if ((p.ProductRealStock <= 3) || (p.ProductQuantityInStock > p.ProductRealStock))
                        more++;

                }
                if (more > 0)
                    OrderDeliveryDate.SelectedDate = Convert.ToDateTime(OrderFormDate.Text).AddDays(3);
                else
                    OrderDeliveryDate.SelectedDate = Convert.ToDateTime(OrderFormDate.Text).AddDays(6);
                currentOrder.OrderDeliveryDate = Convert.ToDateTime(OrderDeliveryDate.Text);
            }
        
        }
    }
