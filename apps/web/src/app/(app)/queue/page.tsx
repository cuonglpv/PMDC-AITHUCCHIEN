import { Suspense } from "react";
import { Loading } from "@/components/states/PageState";
import { ReviewQueuePage } from "@/features/queues/ReviewQueuePage";

export default function ReviewQueue() {
  return (
    <Suspense fallback={<Loading label="Loading the review queue" />}>
      <ReviewQueuePage />
    </Suspense>
  );
}
