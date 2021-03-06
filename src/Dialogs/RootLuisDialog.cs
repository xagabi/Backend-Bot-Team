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
using BackendBot.Repositories;
using BackendBot.Services;
using Microsoft.Bot.Builder.Resource;
using Microsoft.Bot.Builder.FormFlow.Advanced;

namespace BackendBot.Dialogs
{
    [LuisModel("1ccd5054-73d5-40cc-99fc-4648d8ac5067", "279f4f31fa6346219ce61e04a5cb34d6")]
    [Serializable]
    public class RootLuisDialog : LuisDialog<object>
    {
        private const string EntityCredential = "Credential";
        private const string EntityAction = "Action";

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
            await context.PostAsync("Try to type anything to see if I can help you.");
            context.Wait(this.MessageReceived);
        }

        [LuisIntent("Greeting")]
        public async Task ProcessGreeting(IDialogContext context, LuisResult result)
        {
            await context.PostAsync("Hello!");
            context.Done(context);
        }

        [LuisIntent("NegativeAnswer")]
        public async Task ProcessNegativeAnswer(IDialogContext context, LuisResult result)
        {
            await context.PostAsync("Thank you. Have a nice day!");
            context.Done(context);
        }

        [LuisIntent("CredentialsSupport")]
        public async Task FixCredentials(IDialogContext context, IAwaitable<IMessageActivity> activity, LuisResult result)
        {
            var message = await activity;
            await context.PostAsync($"I will help you to retrieve your credentials.");

            EntityRecommendation credentialEntity;

            if (result.TryFindEntity(EntityCredential, out credentialEntity))
            {
                credentialEntity.Type = "Credential";
            }

            if (credentialEntity.Entity == "username")
            {
                await context.PostAsync($"Let's recover your username. Could you please provide your product key or order number?");
                await OnRecoveryDataProvided(context);
            }
            else if (credentialEntity.Entity == "password")
            {
                await context.PostAsync($"Let's recover your password. What is your username?");
                context.Wait(OnRecoveryEmailProvided);
            }
            else
            {
                await context.PostAsync($"Sorry, I do not know what credential {credentialEntity.Entity} is.");
            }
        }

        [LuisIntent("GeneralIntent")]
        public async Task ProcessIntents(IDialogContext context, LuisResult result)
        {
            EntityRecommendation actionEntity;

            if (result.TryFindEntity(EntityAction, out actionEntity))
            {
                actionEntity.Type = "Action";
            }

            if (actionEntity.Entity == "renew")
            {
                await context.PostAsync($"I will help you with your product renewal.");
            }
            else if (actionEntity.Entity == "purchase" || actionEntity.Entity == "buy")
            {
                await BuildProductsCarousel(context);
            }
            else
            {
                await context.PostAsync($"Sorry, I do not recognize {actionEntity.Entity} action.");
            }
            await OnFlowFinished(context);
        }

        [LuisIntent("MaliciousIntent")]
        public async Task ProcessMaliciousIntent(IDialogContext context, LuisResult result)
        {
            await context.PostAsync($"I hope you don't kiss your mother with that mouth.");
            await OnFlowFinished(context);
        }

        [LuisIntent("DisableAR")]
        public async Task DisableAutoRenew(IDialogContext context, LuisResult result)
        {
            await context.PostAsync($"I will help you with disabling autorenewal.");
            await context.PostAsync($" Please provide your email address:");

            context.Wait(this.OnEmailProvided);
        }

        private async Task OnFlowFinished(IDialogContext context)
        {
            await context.PostAsync($"Is there anything else I can help you with?");
            context.Wait(MessageReceived);
        }

        private async Task OnEmailProvided(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            var message = await result;
            var userEmail = message.Text.ToLower();

            if (userEmail == "no")
            {
                await OnFlowFinished(context);
            }
            else
            {
                var user = new InMemoryUserRepository().GetByEmailAddress(userEmail);

                if (user == null)
                {
                    await context.PostAsync($"Couldn't find username {message.Text}. Would you like to try again?");
                    context.Wait(OnEmailProvided);
                }
                else
                {
                    await context.PostAsync($"Hello {user.FullName}! These are your products.");
                    await LoadUserProducts(context, user.EmailAddress);
                }
            }
        }

        private async Task OnUserProductDisableAutoRenewProvided(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            var message = await result;
            var userProduct = UserProductsService.GetUserProductyById(int.Parse(message.Text));
            var product = ProductsService.GetProductById(userProduct.ProductId);
            await context.PostAsync($"The autorenewal on your {product.Name} subscription has been successfully disabled.");
            await OnFlowFinished(context);
            context.Done(userProduct);
        }

        private async Task OnRecoveryEmailProvided(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            var message = await result;
            var userEmail = message.Text.ToLower();


            if (userEmail == "no")
            {
                await OnFlowFinished(context);
            }
            else
            {
                var user = new InMemoryUserRepository().GetByEmailAddress(userEmail);

                if (user == null)
                {
                    await context.PostAsync($"Couldn't find username {message.Text}. Would you like to try again?");
                    context.Wait(OnRecoveryEmailProvided);
                }
                else
                {
                    await context.PostAsync($"Hello {user.FullName}! A password recovery email has been sent.");
                    await OnFlowFinished(context);
                }
            }
        }

        private async Task OnRecoveryDataProvided(IDialogContext context)
        {
            await context.PostAsync("Unfortunately I cannot help you with this. Please contact support.");
            await OnFlowFinished(context);
        }

        private async Task BuildProductsCarousel(IDialogContext context)
        {
            var resultMessage = context.MakeMessage();
            resultMessage.AttachmentLayout = AttachmentLayoutTypes.Carousel;
            resultMessage.Attachments = new List<Attachment>();

            var products = Services.ProductsService.GetProducts();

            foreach (Product product in products)
            {
                HeroCard heroCard = new HeroCard()
                {
                    Title = product.Name,
                    Subtitle = product.Price,
                    Text = product.Description,
                    Images = new List<CardImage>()
                        {
                            new CardImage() { Url = product.Image }
                        },
                    Buttons = new List<CardAction>()
                        {
                            new CardAction()
                            {
                                Title = "Buy",
                                Type = ActionTypes.OpenUrl,
                                Value = "https://www.bullguard.com/shop"
                            }
                        }
                };




                resultMessage.Attachments.Add(heroCard.ToAttachment());
            }

            await context.PostAsync(resultMessage);
            await context.PostAsync("If you choose to buy a product from the list above you get a 20% discount!");
        }

        private async Task LoadUserProducts(IDialogContext context, string emailAddress)
        {
            try
            {
                var userProducts = UserProductsService.GetUserProductyByEmailAddress(emailAddress).Select(up => new { up.UserProductId, up.ProductId, up.OrderId });

                await context.PostAsync($"I found {userProducts.Count()} products:");

                if (userProducts.Count() > 0)
                {
                    var resultMessage = context.MakeMessage();
                    resultMessage.AttachmentLayout = AttachmentLayoutTypes.Carousel;
                    resultMessage.Attachments = new List<Attachment>();

                    foreach (var userProduct in userProducts)
                    {
                        var product = ProductsService.GetProductById(userProduct.ProductId);

                        HeroCard heroCard = new HeroCard()
                        {
                            Title = product.Name,
                            Subtitle = $"Order number: {userProduct.OrderId}",
                            Images = new List<CardImage>()
                        {
                            new CardImage() { Url = product.Image }
                        },
                            Buttons = new List<CardAction>()
                        {
                            new CardAction()
                            {
                                Title = "Select",
                                Type = ActionTypes.PostBack,
                                Value = userProduct.UserProductId
                            }
                        }
                        };

                        resultMessage.Attachments.Add(heroCard.ToAttachment());
                    }

                    await context.PostAsync(resultMessage);
                    context.Wait(OnUserProductDisableAutoRenewProvided);
                }
                else
                {
                    await context.PostAsync("I am sorry, but I could not find any eligible products on your account.");
                    await OnFlowFinished(context);
                }
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
            //finally
            //{
            //    context.Done<object>(null);
            //}
        }

    }
}
