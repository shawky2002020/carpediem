# Independent review of the mobile record screens

## Starting assessment

The previous independent product score was **90/100**. The phone captures showed the clearest remaining problem on availability. A Leader could see “Response Needed,” “Your Schedule Response” and “You have not responded yet,” yet still lack the time and place of the commitment. The first choice began 444 CSS pixels below the top at 390 px. Assignment repeated the entire date and Cairo label in one wrapped line. The appointment presented a same-day interval as two full dates. Feedback and assignment both put an event name in a dark rectangular banner. [Previous availability](../after/availability-390.png) · [Previous assignment](../after/assignment-390.png) · [Previous appointment](../after/appointment-390.png)

I set these review questions before seeing the revised availability layout.

- Can a Leader opening a direct URL identify the event and understand a Cairo time range and venue without a notification? Is a cross-midnight event unambiguous when the device is in another timezone?
- Do a response, an appointment, an assignment and a rating read as different tasks at first glance? Does each keep the site's typography and navigation recognizable?
- Does a delayed or failed response preserve the last committed choice, prevent duplicate writes, present a recovery path and survive reload? Does the focus remain usable?
- Are actions touch-sized on a phone, does 200% text at 320 px retain the whole record, and do native inputs and confirmations remain keyboard-operable?
- Is any improvement in a specific scoring category supported by a changed screen or observed interaction? The original nine weights and the unchanged parts of the product carry forward.

## Decision after inspection

**Independent product score: 95/100.** This is the same nine-part product rubric used for the 90-point baseline. I evaluated the revised screens and interactions in Chromium. For product areas outside the revision, the previous documented assessments carry forward. The five added points correspond to distinct changes observed in this round.

| Criterion | Prior | Current | Evidence and remaining deduction |
| --- | ---: | ---: | --- |
| Shared component craftsmanship | 19/20 | 19/20 | The rating scale has 48 px clickable labels, native radio keys and a visible selection. Error notices still consume substantial vertical space. |
| Page composition and art direction | 14/15 | 15/15 | Availability organizes facts around the date. Appointment pairs an invitation with a decision. Assignment is a timetable. Feedback opens with the person to rate. Incidents opens on the two fields needed to report. On a phone the five screens have visibly different information structures. |
| Originality and brand identity | 13/15 | 14/15 | The paper invitation, compact ochre calendar leaf, ruled timetable and incident margin carry a common field-guide identity without repeating the same colored block. Administrative cancellation and rescheduling are still more conventional. |
| Motion and interaction | 13/15 | 14/15 | A 900 ms write kept choices stationary and repeated keyboard activation sent one request. An ambiguous failure now preserves the original answer and focus, requires a state refresh, and lets the Leader complete the change. Declining an appointment now identifies the selected action and moves focus to its decision receipt. Timing on physical field hardware remains unmeasured. |
| Mobile experience | 9/10 | 10/10 | At 390 px both availability choices now start at y=399 and each measures 175 × 48 px. The old second action began at y=500. The venue, date, time and two actions all fit before scrolling. Enlarged text can stack the actions and all seven checked routes reflow at 320 px with 200% text. |
| Typography and hierarchy | 9/10 | 9/10 | Times, event names and decisions read in the right order. The overnight date range is still dense on a narrow screen with long contextual labels. |
| Task efficiency | 4/5 | 5/5 | The authorized direct entry now answers what, when and where before asking for a declaration. The live SQL rehearsal verified these facts without an inbox list and verified prepublication itinerary withholding. |
| Cross-product consistency | 5/5 | 5/5 | The varied record compositions retain the same role navigation, action hierarchy, focus treatment and time-zone convention. |
| Accessibility and performance | 4/5 | 4/5 | The seven phone axe scans and measured reflow checks passed. Keyboard recovery, duplicate suppression and native focus behavior were exercised. Physical-device use and human screen-reader evaluation remain open. |

### Interaction findings and fixes

The first revised appointment mislabeled the pending action after selecting Decline and dropped focus to the document body when it finished. With the subsequent build, the 800 ms decline displayed “Declining…” on that button, a single write was issued, and the committed decision message received keyboard focus. The assignment cancellation sheet focused the reason field and returned focus to its trigger after Escape. The rating scale accepted Space and Right Arrow, serialized a 3, and moved to the optional comment on Tab. [Appointment receipt](independent/records/appointment-declined-390.png) · [Action checks](independent/records/record-actions.json)

The first revised availability screen also lost keyboard focus on a failed write. I repeated the test after the correction. With the browser set to Los Angeles and the inbox fixture empty, the screen displayed the Cairo event from 23:30 on 12 December through 01:00 on 13 December, together with North orchard gate. Holding the write for 900 ms left both action positions unchanged. An HTTP 503 retained the last confirmed selection and focus on the initiating button. Repeated Enter before reconciliation still left the request count at one. Refresh returned focus to “Can you take part?”. A later successful write persisted after reload. A separate null-itinerary check on an open record displayed a missing-context message. The draft fixture had disabled response choices. [Overnight record](independent/cross-midnight-390.png) · [Failure](independent/failed-write-390.png) · [Recording](independent/delayed-response.webm) · [Measurements](independent/delayed-response.json)

The live rehearsal was performed by the implementation coordinator using a real Leader and SQL Server. I read its result separately from the fixture runs. The draft read returned HTTP 200 with a null itinerary. Direct entry after publication, with notifications held empty, displayed the verified event title, Mango Tree Campsite, and Sunday 7 March 2027, 10:00–14:00 Cairo. The Admin's stored UTC schedule was 08:00–12:00. Ordinary Leader access to the Admin API returned HTTP 403.

### Visual evidence

Seven routes were checked at 390 and 1440 px, plus 320 px with 200% text. I saw no page exceptions, missing fixture requests or document overflow. The WCAG-tagged axe scans ran at the 390 px size. The screenshots and measurements come from deterministic role fixtures. [Availability](independent/availability/availability-390.png) · [Appointment](independent/records/appointment-390.png) · [Assignment](independent/records/assignment-390.png) · [Feedback](independent/records/feedback-390.png) · [Incident report](independent/records/incidents-390.png) · [Record measurements](independent/records/review-data.json) · [Availability measurements](independent/availability/review-data.json)
