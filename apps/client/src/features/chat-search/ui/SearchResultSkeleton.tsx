import { Skeleton } from "@/shared/ui";
import { View } from "react-native";

interface SearchResultSkeletonListProps {
    count?: number;
    showSnippet?: boolean;
}

export const SearchResultSkeleton = ({ showSnippet = true }: Pick<SearchResultSkeletonListProps, "showSnippet">) => {
    return (
        <View testID="search-result-skeleton" className="px-4 py-3 flex-row gap-3">
            <Skeleton className="h-10 w-10 rounded-xl shrink-0" />
            <View className="flex-1 min-w-0 gap-2">
                <View className="flex-row justify-between items-start">
                    <Skeleton className="h-3.5 w-36 max-w-[65%] rounded" />
                    <Skeleton className="h-3 w-12 rounded bg-white/5" />
                </View>
                <Skeleton className="h-3 w-24 max-w-[45%] rounded bg-white/5" />
                {showSnippet && <Skeleton className="h-3 w-full rounded bg-white/5" />}
            </View>
        </View>
    );
};

export const SearchResultSkeletonList = ({
    count = 3,
    showSnippet = true,
}: SearchResultSkeletonListProps) => {
    return (
        <View>
            {Array.from({ length: count }, (_, index) => (
                <SearchResultSkeleton key={index} showSnippet={showSnippet} />
            ))}
        </View>
    );
};
