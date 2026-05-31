import { AuthHeaders, HandleUnauthorized, createRequest } from "./utils";

export type DisciplineRole = "Teacher" | "Student";

export type DisciplinePermissions = {
  canUpdate: boolean;
  canArchive: boolean;
  canManageEnrollments: boolean;
  canManageSubgroups: boolean;
};

export type DisciplineSubgroup = {
  id: string;
  name: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  archivedAtUtc?: string | null;
};

export type DisciplineEnrollment = {
  userId: string;
  role: DisciplineRole;
  subgroupId?: string | null;
  enrolledAtUtc: string;
  enrolledBy: string;
};

export type Discipline = {
  id: string;
  code: string;
  title: string;
  description?: string | null;
  semester: string;
  ownerTeacherId: string;
  coverAssetId?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  archivedAtUtc?: string | null;
  subgroups: DisciplineSubgroup[];
  permissions: DisciplinePermissions;
  enrollments: DisciplineEnrollment[];
};

export type DisciplineListItem = {
  id: string;
  code: string;
  title: string;
  semester: string;
  ownerTeacherId: string;
  coverAssetId?: string | null;
  createdAtUtc: string;
  archivedAtUtc?: string | null;
  permissions: DisciplinePermissions;
};

export type MyDiscipline = {
  id: string;
  code: string;
  title: string;
  semester: string;
  ownerTeacherId: string;
  coverAssetId?: string | null;
  role: DisciplineRole;
  subgroupId?: string | null;
};

export type ListDisciplinesResponse = { items: DisciplineListItem[] };
export type ListMyDisciplinesResponse = { items: MyDiscipline[] };
export type ListSubgroupsResponse = { items: DisciplineSubgroup[] };
export type ListEnrollmentsResponse = {
  items: DisciplineEnrollment[];
  nextCursor?: string | null;
};

const PREFIX = "/api/disciplines";

export function createDisciplinesApi(
  baseUrl: string,
  authHeaders: AuthHeaders,
  handleUnauthorized: HandleUnauthorized
) {
  const request = createRequest(baseUrl, authHeaders, handleUnauthorized);

  return {
    list(params: { semester?: string; includeArchived?: boolean } = {}): Promise<ListDisciplinesResponse> {
      const query = new URLSearchParams();
      if (params.semester) query.set("semester", params.semester);
      if (params.includeArchived) query.set("includeArchived", "true");
      const qs = query.toString();
      return request<ListDisciplinesResponse>(`${PREFIX}${qs ? `?${qs}` : ""}`);
    },

    listMine(): Promise<ListMyDisciplinesResponse> {
      return request<ListMyDisciplinesResponse>(`${PREFIX}/me`);
    },

    get(id: string): Promise<Discipline> {
      return request<Discipline>(`${PREFIX}/${encodeURIComponent(id)}`);
    },

    listSubgroups(id: string, includeArchived = false): Promise<ListSubgroupsResponse> {
      const query = includeArchived ? "?includeArchived=true" : "";
      return request<ListSubgroupsResponse>(`${PREFIX}/${encodeURIComponent(id)}/subgroups${query}`);
    },

    listEnrollments(id: string, cursor?: string, limit?: number): Promise<ListEnrollmentsResponse> {
      const query = new URLSearchParams();
      if (cursor) query.set("cursor", cursor);
      if (limit) query.set("limit", String(limit));
      const qs = query.toString();
      return request<ListEnrollmentsResponse>(
        `${PREFIX}/${encodeURIComponent(id)}/enrollments${qs ? `?${qs}` : ""}`,
      );
    },

  };
}
