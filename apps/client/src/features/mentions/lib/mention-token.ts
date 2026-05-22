// Находит mention-токен (`@frag`), в котором сейчас стоит курсор. Возвращает
// null, если курсор не в токене, иначе [start, end] границы и query без `@`.
// Считаем токеном последовательность `\w` и кириллицы, начинающуюся с `@`,
// не разрывающуюся пробелом — это позволяет пользователю продолжать набирать
// «@иван иванов» (но в этом случае к моменту пробела автокомплит уже закроем).

const MENTION_CHAR_RE = /[A-Za-zА-Яа-яЁё0-9_.-]/;

export interface MentionToken {
    /** Индекс символа `@` в строке. */
    start: number;
    /** Конец токена (эксклюзивно). Совпадает с cursor, пока пользователь печатает. */
    end: number;
    /** Подстрока после `@` для фильтрации списка участников. */
    query: string;
}

export function findMentionAtCursor(text: string, cursor: number): MentionToken | null {
    if (cursor <= 0 || cursor > text.length) return null;

    // Идём назад от курсора, пока встречаем mention-символы.
    let i = cursor - 1;
    while (i >= 0 && MENTION_CHAR_RE.test(text[i]!)) {
        i--;
    }

    // Перед `@` должен быть либо начало строки, либо пробел/перенос.
    if (i < 0 || text[i] !== "@") return null;
    if (i > 0 && !/\s/.test(text[i - 1]!)) return null;

    return {
        start: i,
        end: cursor,
        query: text.slice(i + 1, cursor),
    };
}
