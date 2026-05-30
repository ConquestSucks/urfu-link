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

export type CreateDisciplineRequest = {
  code: string;
  title: string;
  description?: string | null;
  semester: string;
  ownerTeacherId: string;
  coverAssetId?: string | null;
};

export type UpdateDisciplineRequest = Omit<CreateDisciplineRequest, "ownerTeacherId">;

export type EnrollmentInput = {
  userId: string;
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
export type EnrollUsersResponse = { enrollments: DisciplineEnrollment[] };

const PREFIX = "/api/disciplines";

const withJsonBody = (body: unknown): RequestInit => ({
  method: "POST",
  body: JSON.stringify(body),
});

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

    create(input: CreateDisciplineRequest, idempotencyKey = crypto.randomUUID()): Promise<Discipline> {
      return request<Discipline>(PREFIX, {
        ...withJsonBody(input),
        headers: { "Idempotency-Key": idempotencyKey },
      });
    },

    update(id: string, input: UpdateDisciplineRequest): Promise<void> {
      return request<void>(`${PREFIX}/${encodeURIComponent(id)}`, {
        method: "PUT",
        body: JSON.stringify(input),
      });
    },

    archive(id: string): Promise<void> {
      return request<void>(`${PREFIX}/${encodeURIComponent(id)}`, { method: "DELETE" });
    },

    listSubgroups(id: string, includeArchived = false): Promise<ListSubgroupsResponse> {
      const query = includeArchived ? "?includeArchived=true" : "";
      return request<ListSubgroupsResponse>(`${PREFIX}/${encodeURIComponent(id)}/subgroups${query}`);
    },

    createSubgroup(id: string, name: string): Promise<DisciplineSubgroup> {
      return request<DisciplineSubgroup>(`${PREFIX}/${encodeURIComponent(id)}/subgroups`, withJsonBody({ name }));
    },

    updateSubgroup(id: string, subgroupId: string, name: string): Promise<DisciplineSubgroup> {
      return request<DisciplineSubgroup>(
        `${PREFIX}/${encodeURIComponent(id)}/subgroups/${encodeURIComponent(subgroupId)}`,
        {
          method: "PATCH",
          body: JSON.stringify({ name }),
        },
      );
    },

    deleteSubgroup(id: string, subgroupId: string): Promise<void> {
      return request<void>(
        `${PREFIX}/${encodeURIComponent(id)}/subgroups/${encodeURIComponent(subgroupId)}`,
        { method: "DELETE" },
      );
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

    enrollUsers(id: string, enrollments: EnrollmentInput[], idempotencyKey = crypto.randomUUID()): Promise<EnrollUsersResponse> {
      return request<EnrollUsersResponse>(`${PREFIX}/${encodeURIComponent(id)}/enrollments`, {
        ...withJsonBody({ enrollments }),
        headers: { "Idempotency-Key": idempotencyKey },
      });
    },

    unenrollUser(id: string, userId: string): Promise<void> {
      return request<void>(
        `${PREFIX}/${encodeURIComponent(id)}/enrollments/${encodeURIComponent(userId)}`,
        { method: "DELETE" },
      );
    },

    changeEnrollmentRole(id: string, userId: string, role: DisciplineRole, subgroupId?: string | null): Promise<void> {
      return request<void>(
        `${PREFIX}/${encodeURIComponent(id)}/enrollments/${encodeURIComponent(userId)}/role`,
        {
          method: "PATCH",
          body: JSON.stringify({ role, subgroupId }),
        },
      );
    },

    assignEnrollmentSubgroup(id: string, userId: string, subgroupId: string): Promise<void> {
      return request<void>(
        `${PREFIX}/${encodeURIComponent(id)}/enrollments/${encodeURIComponent(userId)}/subgroup`,
        {
          method: "PATCH",
          body: JSON.stringify({ subgroupId }),
        },
      );
    },
  };
}
