using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Mvc;
using PayPal.PayPalAPIInterfaceService;
using PayPal.PayPalAPIInterfaceService.Model;
using PayPalSample.Models;

namespace PayPalSample.Controllers
{
    public class PayPalLegacyController : Controller
    {
        //
        // GET: /PayPalLegacy/

        public ActionResult Index()
        {
            return View();
        }

        public ActionResult CreateAgreement()
        {
            return View();
        }

        [HttpPost]
        public ActionResult AuthorizeAgreement()
        {
            var service = new PayPalAPIInterfaceServiceService();

            var request = new SetExpressCheckoutReq()
            {
                SetExpressCheckoutRequest = new SetExpressCheckoutRequestType
                {
                    SetExpressCheckoutRequestDetails = new SetExpressCheckoutRequestDetailsType()
                    {
                        BillingAgreementDetails = new List<BillingAgreementDetailsType>()
                        {
                            new BillingAgreementDetailsType(BillingCodeType.MERCHANTINITIATEDBILLINGSINGLEAGREEMENT)
                            {
                                BillingAgreementDescription = "All your money are belong to us.",
                            }
                        },
                        ReturnURL = Utilities.ToAbsoluteUrl(HttpContext, Url.Action("AgreementConfirmed")),
                        CancelURL = Utilities.ToAbsoluteUrl(HttpContext, Url.Action("AgreementCanceled")),
                        BuyerEmail = "nathan@nathantaylor.com",
                        BrandName = "Super Legitimate Website"
                    }
                }
            };

            var response = service.SetExpressCheckout(request);

            var url = String.Format("https://www.sandbox.paypal.com/webscr&cmd=_express-checkout&token={0}", response.Token);

            return Redirect(url);
        }

        public ActionResult AgreementConfirmed(string token)
        {
            var service = new PayPalAPIInterfaceServiceService();

            var request = new CreateBillingAgreementReq
            {
                CreateBillingAgreementRequest = new CreateBillingAgreementRequestType(token)
            };

            var response = service.CreateBillingAgreement(request);

            if (response.Ack == AckCodeType.SUCCESS)
            {
                var billingAgreementId = response.BillingAgreementID;

                // store the billing agreement id in a cookie
                Response.Cookies.Add(new HttpCookie("pp_aid", billingAgreementId) { Expires = DateTime.Now.AddDays(365) });

                return View(new AgreementConfirmedViewData { BillingAgreementId = billingAgreementId });
            }
            else
            {
                foreach (var error in response.Errors)
                {
                    ModelState.AddModelError("__FORM", error.LongMessage);
                }
            }

            return View("Error");
        }

        public ActionResult CancelAgreement()
        {
            var billingAgreementId = Request.Cookies["pp_aid"] != null ? Request.Cookies["pp_aid"].Value : null;

            return View(new CancelAgreementViewData { BillingAgreementId = billingAgreementId });
        }

        [HttpPost]
        public ActionResult CancelAgreement(string billingAgreementId)
        {
            var service = new PayPalAPIInterfaceServiceService();

            var request = new BillAgreementUpdateReq
            {
                BAUpdateRequest = new BAUpdateRequestType
                {
                    ReferenceID = billingAgreementId,
                    BillingAgreementStatus = MerchantPullStatusCodeType.CANCELED
                }
            };

            var response = service.BillAgreementUpdate(request);

            if (response.Ack == AckCodeType.SUCCESS)
            {
                // clear billing agreement id cookie
                Response.Cookies.Add(new HttpCookie("pp_aid", null) { Expires = DateTime.Now.AddDays(-1) });

                return RedirectToAction("AgreementCanceled");
            }
            else
            {
                foreach (var error in response.Errors)
                {
                    ModelState.AddModelError("__FORM", error.LongMessage);
                }
            }

            return View("Error");
        }

        public ActionResult AgreementCanceled()
        {
            return View();
        }

        public ActionResult CreateOrder(string billingAgreementId)
        {
            billingAgreementId = billingAgreementId ?? (Request.Cookies["pp_aid"] != null ? Request.Cookies["pp_aid"].Value : null);

            return View(new CreateOrderViewData { BillingAgreementId = billingAgreementId });
        }

        [HttpPost]
        public ActionResult CreateOrder(string billingAgreementId, decimal amount, string description = null, bool capture = false)
        {
            var service = new PayPalAPIInterfaceServiceService();

            var request = new DoReferenceTransactionReq
            {
                DoReferenceTransactionRequest = new DoReferenceTransactionRequestType
                {
                    DoReferenceTransactionRequestDetails = new DoReferenceTransactionRequestDetailsType
                    {
                        PaymentAction = PaymentActionCodeType.AUTHORIZATION,
                        ReferenceID = billingAgreementId,
                        PaymentDetails = new PaymentDetailsType
                        {
                            OrderTotal = new BasicAmountType(CurrencyCodeType.USD, amount.ToString("f2")),
                            OrderDescription = description ?? "This is a thing that you bought"
                        }
                    }
                }
            };

            var response = service.DoReferenceTransaction(request);

            if (response.Ack == AckCodeType.SUCCESS)
            {
                var authorizationId = response.DoReferenceTransactionResponseDetails.PaymentInfo.TransactionID;

                if (capture)
                {
                    return CaptureOrder(authorizationId);
                }

                return RedirectToAction("OrderAuthorized", new { authorizationId = authorizationId });
            }
            else
            {
                foreach (var error in response.Errors)
                {
                    ModelState.AddModelError("__FORM", error.LongMessage);
                }
            }

            return View("Error");
        }

        public ActionResult OrderAuthorized(string authorizationId)
        {
            return View(new AuthorizedViewData { AuthorizationId = authorizationId });
        }

        [HttpPost]
        public ActionResult CaptureOrder(string authorizationId)
        {
            var service = new PayPalAPIInterfaceServiceService();

            var request = new GetTransactionDetailsReq
            {
                GetTransactionDetailsRequest = new GetTransactionDetailsRequestType
                {
                    TransactionID = authorizationId
                }
            };

            var transactionDetailsResponse = service.GetTransactionDetails(request);

            if (transactionDetailsResponse.Ack == AckCodeType.SUCCESS)
            {
                //if(transactionDetailsResponse.PaymentTransactionDetails.PaymentInfo.PaymentStatus == PaymentStatusCodeType.)

                var captureRequest = new DoCaptureReq
                {
                    DoCaptureRequest = new DoCaptureRequestType
                    {
                        AuthorizationID = authorizationId,
                        Amount = transactionDetailsResponse.PaymentTransactionDetails.PaymentInfo.GrossAmount
                    }
                };

                var captureResponse = service.DoCapture(captureRequest);

                if (captureResponse.Ack == AckCodeType.SUCCESS)
                {
                    return RedirectToAction("OrderSuccessful", new { transactionId = captureResponse.DoCaptureResponseDetails.PaymentInfo.TransactionID });
                }
                else
                {
                    foreach (var error in captureResponse.Errors)
                    {
                        ModelState.AddModelError("__FORM", error.LongMessage);
                    }
                }
            }
            else
            {
                foreach (var error in transactionDetailsResponse.Errors)
                {
                    ModelState.AddModelError("__FORM", error.LongMessage);
                }
            }

            return View("Error");
        }

        public ActionResult VoidOrder()
        {
            return View();
        }

        [HttpPost]
        public ActionResult VoidOrder(string authorizationId)
        {
            var service = new PayPalAPIInterfaceServiceService();

            var request = new GetTransactionDetailsReq
            {
                GetTransactionDetailsRequest = new GetTransactionDetailsRequestType
                {
                    TransactionID = authorizationId
                }
            };

            var transactionDetailsResponse = service.GetTransactionDetails(request);

            if (transactionDetailsResponse.Ack == AckCodeType.SUCCESS)
            {
                //if(transactionDetailsResponse.PaymentTransactionDetails.PaymentInfo.PaymentStatus == PaymentStatusCodeType.)

                var voidRequest = new DoVoidReq()
                {
                    DoVoidRequest = new DoVoidRequestType
                    {
                        AuthorizationID = authorizationId,
                    }
                };

                var voidResponse = service.DoVoid(voidRequest);

                if (voidResponse.Ack == AckCodeType.SUCCESS)
                {
                    return RedirectToAction("OrderVoided", new { transactionId = authorizationId });
                }
                else
                {
                    foreach (var error in voidResponse.Errors)
                    {
                        ModelState.AddModelError("__FORM", error.LongMessage);
                    }
                }
            }
            else
            {
                foreach (var error in transactionDetailsResponse.Errors)
                {
                    ModelState.AddModelError("__FORM", error.LongMessage);
                }
            }

            return View("Error");
        }

        public ActionResult OrderSuccessful(string transactionId)
        {
            var viewData = new OrderConfirmedViewData
            {
                TransactionId = transactionId
            };

            return View(viewData);
        }

        public ActionResult OrderCanceled()
        {
            return View();
        }

        public ActionResult OrderVoided(string transactionId)
        {
            return View(new OrderVoidedViewData { TransactionId = transactionId });
        }

        public ActionResult ViewTransaction()
        {
            return View();
        }

        public PartialViewResult TransactionDetails(string transactionId)
        {
            var service = new PayPalAPIInterfaceServiceService();

            var request = new GetTransactionDetailsReq
            {
                GetTransactionDetailsRequest = new GetTransactionDetailsRequestType
                {
                    TransactionID = transactionId
                }
            };

            var response = service.GetTransactionDetails(request);

            if (response.Ack == AckCodeType.SUCCESS)
            {
                var viewData = new TransactionViewData
                {
                    TransactionId = response.PaymentTransactionDetails.PaymentInfo.TransactionID,
                    Payer = response.PaymentTransactionDetails.PayerInfo.Payer,
                    DateTime = response.PaymentTransactionDetails.PaymentInfo.PaymentDate,
                    Status = response.PaymentTransactionDetails.PaymentInfo.PaymentStatus.ToString(),
                    Total = Convert.ToDecimal(response.PaymentTransactionDetails.PaymentInfo.GrossAmount.value)
                };

                return PartialView("_TransactionDetails", viewData);
            }

            return null;
        }
    }
}
