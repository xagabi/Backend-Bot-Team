﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.FormFlow;
using Microsoft.Bot.Builder.Luis;
using Microsoft.Bot.Builder.Luis.Models;
using Microsoft.Bot.Connector;
using BackendBot.Models;
using LuisBot.Repositories;

namespace BackendBot.Dialogs
{


    [LuisModel("1ccd5054-73d5-40cc-99fc-4648d8ac5067", "279f4f31fa6346219ce61e04a5cb34d6")]
    [Serializable]
    public class RootLuisDialog : LuisDialog<object>
    {
        public string EntityProductName { get; private set; }
        private ResumptionCookie resumptionCookie;

        public virtual async Task MessageReceivedAsync(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            var message = await result;

            if (this.resumptionCookie == null)
            {
                this.resumptionCookie = new ResumptionCookie(message);
            }
        }

        [LuisIntent("")]
        [LuisIntent("None")]
        public async Task None(IDialogContext context, LuisResult result)
        {
            string message = $"Sorry, I did not understand '{result.Query}'. Type 'help' if you need assistance.";

            await context.PostAsync(message);

            context.Wait(this.MessageReceived);
        }

        [LuisIntent("Help")]
        public async Task Help(IDialogContext context, LuisResult result)
        {
            await context.PostAsync("Hi! Try asking me things like 'I want a refund', 'I want to renew my subscription' or 'show me products'");

            context.Wait(this.MessageReceived);
        }

        //[LuisIntent("Renew")]
        //public async Task Search(IDialogContext context, IAwaitable<IMessageActivity> activity, LuisResult result)
        //{
        //    var message = await activity;
        //    await context.PostAsync($"I will help you with a refund. Please give me your email address.");

        //    var productsQuery = new ProductQuery();
        //    var productsFormDialog = new FormDialog<ProductQuery>(productsQuery, this.BuildProductsForm, FormOptions.PromptInStart, result.Entities);

        //    context.Call(productsFormDialog, this.ResumeAfterProductsFormDialog);
        //}

        [LuisIntent("Refund")]
        public async Task Refund(IDialogContext context, LuisResult result)
        {
            await context.PostAsync($"I will help you with refund your products. Please provide your email address.");

            context.Wait(this.OnEmailProvided);
        }

        private async Task OnEmailProvided(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            var message = await result;
            var userEmail = message.Text;
            var user = new InMemoryUserRepository().GetByEmailAddress(userEmail);

            if(user == null)
            {
                await context.PostAsync($"Couldn't find username. Would you like to try again?");
            }
            else
            {
                await context.PostAsync($"Hello {user.FullName}! These are your products.");
            }
        }

        private async Task BuildProductsCarousel(IDialogContext context)
        {
            var resultMessage = context.MakeMessage();
            resultMessage.AttachmentLayout = AttachmentLayoutTypes.Carousel;
            resultMessage.Attachments = new List<Attachment>();

            var products = this.GetProducts();

            foreach (Product product in products)
            {
                ThumbnailCard thumbnailCard = new ThumbnailCard()
                {
                    Title = product.Name,
                    Text = product.Description,
                    Subtitle = product.Price,
                    Images = new List<CardImage>()
                        {
                            new CardImage() { Url = product.Image }
                        },
                };

                resultMessage.Attachments.Add(thumbnailCard.ToAttachment());
            }

            await context.PostAsync(resultMessage);
        }

        private IForm<ProductQuery> BuildProductsForm()
        {
            OnCompletionAsyncDelegate<ProductQuery> processProductsSearch = async (context, state) =>
            {
                var message = "Searching for products";
                if (!string.IsNullOrEmpty(state.ClientIdentifier))
                {
                    message += $" in {state.ClientIdentifier}...";
                }
                else if (!string.IsNullOrEmpty(state.ProductId))
                {
                    message += $" in {state.ProductId.ToUpperInvariant()}...";
                }

                await context.PostAsync(message);
            };

            return new FormBuilder<ProductQuery>()
                .Field(nameof(ProductQuery.ClientIdentifier), (state) => string.IsNullOrEmpty(state.ProductId))
                .Field(nameof(ProductQuery.ProductId), (state) => string.IsNullOrEmpty(state.ClientIdentifier))
                .OnCompletion(processProductsSearch)
                .Build();
        }

        private async Task ResumeAfterProductsFormDialog(IDialogContext context, IAwaitable<ProductQuery> result)
        {
            try
            {
                var products = this.GetProducts();

                await context.PostAsync($"I found {products.Count()} products:");

                var resultMessage = context.MakeMessage();
                resultMessage.AttachmentLayout = AttachmentLayoutTypes.Carousel;
                resultMessage.Attachments = new List<Attachment>();

                foreach (var product in products)
                {
                    HeroCard heroCard = new HeroCard()
                    {
                        Title = product.Name,
                        Subtitle = $"{product.Description}",
                        Images = new List<CardImage>()
                        {
                            new CardImage() { Url = product.Image }
                        },
                        Buttons = new List<CardAction>()
                        {
                            new CardAction()
                            {
                                Title = "More details",
                                Type = ActionTypes.OpenUrl,
                                Value = $"https://www.bullguard.com/shop/"
                            }
                        }
                    };

                    resultMessage.Attachments.Add(heroCard.ToAttachment());
                }

                await context.PostAsync(resultMessage);
            }
            catch (FormCanceledException ex)
            {
                string reply;

                if (ex.InnerException == null)
                {
                    reply = "You have canceled the operation.";
                }
                else
                {
                    reply = $"Oops! Something went wrong :( Technical Details: {ex.InnerException.Message}";
                }

                await context.PostAsync(reply);
            }
            finally
            {
                context.Done<object>(null);
            }
        }

        private IEnumerable<Product> GetProducts()
        {
            var products = new InMemoryProductRepository().Find(null);
            return products;
        }
    }
}
