using RhythmFlow.Api.Dtos;

namespace RhythmFlow.Api.Services;

/// <summary>
/// The client's own policy wording (feedback of 20 Oct 2026), word for word. The app, the web version and the booking
/// confirmation emails all read it from here, so every place shows the same text. Change the wording here, not in the apps.
/// </summary>
public static class PolicyContent
{
    public const string SubscriptionCancellation = "subscription-cancellation";
    public const string SubscriptionRenewal = "subscription-renewal";
    public const string EventCancellation = "event-cancellation";
    public const string OnlineTechnical = "online-technical";

    public static readonly IReadOnlyList<PolicyDto> All =
    [
        new(SubscriptionCancellation, "Subscription Cancellation Policy", """
            We understand that your needs and circumstances may change. Our subscription cancellation process is designed to be straightforward, transparent, and fair.

            1. How to cancel your subscription
            You may request cancellation through your account settings in the Rhythm & Flow app, where available, or by contacting Rhythm & Flow using our published support details.
            Please follow the cancellation instructions and retain the confirmation for your records. Rhythm & Flow will confirm the effective cancellation date and any remaining payment obligations.

            2. When cancellation takes effect
            For monthly subscriptions, cancellation will ordinarily prevent the next renewal, with access continuing until the end of the paid subscription period.
            For annual subscriptions: If you do not wish your annual subscription to continue beyond its agreed term, please submit your cancellation instruction before the applicable expiry or renewal date, allowing sufficient time for processing. This does not limit any statutory cancellation rights.

            3. Refunds for unused subscription time
            Subscription fees are not ordinarily refunded solely because you have not used the app or its content during a paid subscription period.
            However, refunds or credits will be considered where required by law, where Rhythm & Flow has materially failed to provide the subscribed service, or where exceptional circumstances justify a fair resolution.

            4. Access after cancellation
            Unless otherwise required by law or agreed with you, your access to subscription content will continue until the end of the period for which you have paid.
            Once your access expires, subscriber-only videos and programmes will no longer be available. Any content specifically purchased separately will remain subject to the terms applicable to that purchase.

            5. Upgrading or downgrading
            If you wish to change your membership tier, you may do so where the app supports the change. The app should clearly display any price difference, effective date, and impact on your content access before the change is confirmed.

            6. Once-off bookings
            Cancelling a subscription does not automatically cancel a separately purchased class, workshop, coaching session, or retreat booking. These purchases are governed by the relevant booking and cancellation terms.

            7. Your consumer rights
            Rhythm & Flow will handle cancellation requests in accordance with applicable South African consumer protection law. Where statutory rights provide for cancellation, refunds, notice periods, or other remedies, those rights take precedence over any inconsistent provision in this policy.
            """),

        new(SubscriptionRenewal, "Subscription Renewal Policy", """
            Rhythm & Flow offers subscription access to its on-demand wellness content through Rhythm Library and Rhythm & Flow Plus. This policy explains how subscription billing, renewals, and payment issues are handled.

            1. Subscription options
            The available membership options are:
            - Rhythm Library: R149 per month, providing access to the subscriber video library.
            - Rhythm & Flow Plus: R249 per month, providing access to the subscriber video library and eligible structured programmes.
            Discounted annual subscription options may also be offered. The applicable price, subscription duration, renewal arrangements, and payment terms will be displayed before checkout.
            Classes, workshops, coaching sessions, and retreats purchased separately are not included in a membership unless expressly stated.

            2. Monthly subscriptions
            Monthly subscriptions are billed in advance and ordinarily renew each month on the relevant billing date until cancelled.
            By subscribing, you authorise the applicable recurring payment in accordance with the terms presented at checkout. Your renewal date and subscription price will be displayed in your account or confirmation email.
            To avoid a subsequent renewal charge, cancel before the next billing date and allow sufficient time for the cancellation to be processed. Any statutory cancellation rights remain unaffected.

            3. Annual subscriptions
            Annual subscriptions are paid in advance for the stated subscription period. The total annual price and applicable terms will be disclosed before payment.
            Before an annual subscription expires, Rhythm & Flow will provide any expiry or renewal notices required by applicable law and explain the available options.
            Where the Consumer Protection Act's fixed-term agreement provisions apply, the subscription will continue on a month-to-month basis after expiry unless the consumer instructs Rhythm & Flow to terminate it at expiry or expressly agrees to renew for a further fixed term, subject to applicable law.
            Any proposed changes to the subscription price or other material terms will be communicated in accordance with applicable legal requirements.

            4. Failed or declined payments
            If a renewal payment fails, Rhythm & Flow may notify you and request that you update your payment method or resolve the issue.
            Where supported, the app may retry the payment after notifying you of the failed transaction. The retry schedule and any applicable grace period will be communicated clearly.
            If payment remains outstanding, access to subscriber-only content may be suspended until payment is resolved, subject to applicable law and the subscription terms. A failed payment must not be treated as a successful renewal.

            5. Changes to membership prices
            If Rhythm & Flow changes its subscription prices, affected members will be notified in advance and in accordance with applicable law.
            Any price change will be applied transparently, with the effective date clearly communicated. Where required, members will be given the opportunity to cancel or exercise any other applicable rights before the new price takes effect.

            6. Payment confirmations and records
            The app should provide a confirmation of each successful subscription payment and make relevant billing information available in the member's account or through email.
            Members should be able to view their current membership tier, subscription status, next billing or expiry date, and applicable price.

            7. Your consumer rights
            All renewals, payment collections, cancellations, and changes to subscription terms are subject to applicable South African consumer protection law. Nothing in this policy limits any rights available to a consumer under applicable law.
            """),

        new(EventCancellation, "Event Cancellation Policy", """
            Customer cancellation
            For cancellation requests received at least 48 hours before the event, the customer can choose a refund or account credit for the full event fee including any VAT etc.
            For requests received less than 48 hours before the event, a refund or credit is not guaranteed; each request will be handled fairly and in line with applicable consumer rights and the published event terms. A maximum of 50% of the event fee could be agreed upon depending on the circumstances.

            Organizer cancellation
            If Rhythm & Flow cancels an event, customers will receive a full refund or, if they prefer, transfer the booking to a rescheduled date or another comparable event. If an event is rescheduled, all ticket holders are notified and offered a clear choice to accept the new date or request a refund/account credit.

            Online/technical issues
            If/where a significant Zoom or provider-side technical problem prevents delivery or materially disrupts a class, we will offer a replacement class or a refund/account credit depending on the circumstances. The practical criteria are in the policy "Online Classes: Technical Issues and Disruptions".

            Account credits
            Account credits issued under this policy will be valid for 12 months from the date of issue. The credit amount, issue date, expiry date, and redemption terms will be communicated to the participant. Account credit will not be used as a substitute for a refund where a refund is required by law.
            """),

        new(OnlineTechnical, "Online Classes: Technical Issues and Disruptions", """
            Rhythm & Flow will make reasonable efforts to ensure that online classes are delivered as advertised. However, occasional technical problems may occur.

            1. Significant technical disruptions
            If a significant technical problem affecting Rhythm & Flow, Zoom, or another service provider prevents a class from starting, causes its cancellation, or materially disrupts its delivery, the participant may choose an appropriate remedy: a replacement class, a refund, or account credit, subject to applicable consumer law and the circumstances.
            A disruption will generally be considered significant if:
            - The class cannot start within 15 minutes of its scheduled start time because of an organiser-side or provider-side technical issue.
            - An interruption lasts approximately 10 minutes or longer and materially affects participation.
            - The class is cancelled, ends substantially earlier than scheduled, or a meaningful portion of the advertised session cannot be delivered.
            These timeframes are practical guidelines rather than automatic rules. The actual impact of the disruption and the circumstances of the class will also be considered.

            2. Minor technical interruptions
            Brief technical glitches that do not materially affect the class will not ordinarily qualify for a refund or account credit. Repeated interruptions will be assessed cumulatively where they affect the participant's experience.

            3. Individual participant technical issues
            Participants are responsible for having a suitable device, access to the relevant meeting link, and a reasonably stable internet connection.
            Where the class is delivered successfully but an individual participant experiences problems with their own internet connection, device, software, or local setup, a refund or account credit will not ordinarily be provided solely for that reason. Exceptional circumstances may be considered fairly.

            4. Reporting a technical issue
            Participants should notify Rhythm & Flow as soon as reasonably possible and, where practicable, within 48 hours of the affected class.
            The report should include the event name and date, booking reference, a description of the issue, and the approximate duration of the disruption. The reporting guideline will not be used to exclude or restrict any rights or remedies available under applicable law.

            5. Replacement classes
            Where a replacement class is selected or agreed upon, Rhythm & Flow will aim to arrange it within 30 days of the affected class. If a suitable replacement cannot reasonably be arranged within that period, Rhythm & Flow will contact the participant to agree on an appropriate alternative or process the participant's chosen refund or account credit, as applicable.

            6. Account credits
            Account credits issued under this policy will be valid for 12 months from the date of issue. The credit amount, issue date, expiry date, and redemption terms will be communicated to the participant.
            Account credit will not be used as a substitute for a refund where a refund is required by law.

            7. Communication and resolution
            Rhythm & Flow will review reported issues, assess the extent of the disruption, and communicate the available resolution as soon as reasonably practicable. Where possible, the instructor will provide an alternative meeting link or practical assistance during the class.
            Nothing in this policy limits any rights a participant may have under applicable South African consumer protection law.
            """),
    ];

    public static PolicyDto? Find(string id) => All.FirstOrDefault(p => p.Id == id);
}
